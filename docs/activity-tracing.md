# Activity tracing and live events

Milestone 7 adds deterministic W3C/OpenTelemetry tracing, retained inspection and
one-way SSE delivery. No LLM, collector or UI animation is required. Audit events
remain separate and unchanged.

## Instrumentation and boundaries

`IActivityPublisher` and its disposable `IActivityScope` live in Application.
`RunAsync` creates a span, emits start/completion/failure events and preserves
cancellation. `Publish` emits an extensible event in the current operation.
Application code depends on neither SSE nor PostgreSQL. Hosts register the shared
implementation through `AddBrain`.

`System.Diagnostics.ActivitySource` (`HomelabBrain`) and the OpenTelemetry SDK
provide W3C `traceparent`/`tracestate` propagation, ASP.NET Core instrumentation
and native HttpClient spans. An incoming valid trace context is continued even
when its sampled flag is off; missing/invalid context creates a new trace.
The authenticated HTTP operation returns `X-Trace-Id`. Trace baggage is not an
application identity or authorization source; the configured OTel propagator
does not propagate baggage.

The current instrumentation covers:

| Surface | Activity |
| --- | --- |
| Authenticated HTTP operations | `agent.request.started/completed/failed`, route templates only |
| Unified and registry search | `brain.search.started/completed/failed`; partial source failures are explicit |
| Registry discovery | `brain.capability.discovered/denied`, including existing permission-denial audits |
| Skill instruction reads | `brain.skill.read.started/completed/failed`, `skill.selected` and skill-to-required-tool correlation |
| MCP tool requests | `mcp.request.started/completed/failed`, with nested `tool.invoked/completed/failed` |
| Optional semantic adapter | Tool and external-service scopes around provider HTTP/response validation |
| Native outbound HTTP | Child `external.request.started/completed/failed`, method/status only |
| Every scheduled worker job | `worker.job.started/completed/failed`, including partial scan failures and retention |

Brain still does **not execute skills or arbitrary registry tools**. Reading
instructions is selection, not `skill.started` or a deployment. Future trusted
executors can use the same publisher to emit skill/tool lifecycle events around
real operations. Adapter spans retain internal correlation even if a remote
service ignores `traceparent`. HTTP spans below an adapter are distinct from the
adapter's full request/response-validation span.

Graph references use the existing registry keys `skill:<id>` / `tool:<id>` and
authenticated identity `agent:<id>`. `brain`, `worker:brain-worker` and
`service:metadata-provider` identify components without indexed graph entities.
Events carry `sourceEntityId`/`targetEntityId`, never create static relationships,
and retain independent trace/span IDs when concurrent flows use the same nodes.

Health, schema and trace/stream inspection do not create application activity
events, avoiding a feedback loop. Anonymous/rejected authentication is not
retained as an agent trace. HTTP authentication and existing audit behavior are
not replaced by telemetry.

## Live API

`GET /api/activity/stream` requires **both** `activity.read` and `activity.live`.
Use the same bound `X-Agent-Id` and bearer credentials as other API endpoints.
Browser clients need authenticated streaming `fetch`; native `EventSource`
cannot set those headers.

Optional query filters are conjunctive:
`agent`, `trace`, `entity`, `eventType`, `status`, `from`, `to`, `tool`, `skill`,
`service`. IDs for `tool`/`skill`/`service` omit the corresponding entity prefix.
Event types are strings, not a closed enumeration. Status values are `started`,
`running`, `succeeded`, `failed`, `cancelled`. Time bounds are inclusive ISO 8601
UTC timestamps. SSE uses named `activity` events with a camelCase JSON payload:

```text
retry: 3000
: connected

id: <event UUID>
event: activity
data: {"traceId":"<32 hex>","spanId":"<16 hex>","parentSpanId":"<16 hex>",...}

```

The response disables buffering/cache and sends heartbeats every 15 seconds.
At that interval the server reloads identity and grants; disabling an agent,
revoking live access or changing system-wide scope terminates its stream.
`stream.error` signals access changes or an interrupted/overflowed subscription.
Disconnect/cancellation releases the subscription and database connection.

PostgreSQL `LISTEN`/`NOTIFY` transports sanitized, bounded events between API and
worker processes and across API replicas. Each SSE subscription uses one
dedicated connection. Plan database connection limits accordingly. This needs a
direct or session-pooled PostgreSQL connection; transaction-pooling proxies are
not suitable for `LISTEN`. Live delivery is best effort, not a durable queue:
there is no `Last-Event-ID` replay. Subscribe before starting an operation;
reconnect and query retained traces after a delivery gap.

## Retained inspection and authorization

| API | Behavior |
| --- | --- |
| `GET /api/traces` | Active/completed summaries, newest first |
| `GET /api/traces/{traceId}` | A matching visible summary, otherwise 404 |
| `GET /api/traces/{traceId}/events` | Bounded events, timestamp order; reconstruct spans from span/parent IDs |

All three require `traces.read`. Filters match the live API; `status` on the
summary list filters aggregate trace status, while event/stream queries filter
individual event status. `limit` defaults to 100 and permits 1-1000. Narrow
`from`/`to` when inspecting longer traces. Trace/event inspection never expands
visibility.

An agent sees only its own traces, even if another caller supplies the same
incoming trace ID. Ownership is partitioned by `(traceId, agentId)`, independent
of headers, baggage and supplied query filters. `traces.read.all` explicitly
allows system-wide inspection/streaming; no role-name bypass exists. For a trace
shared by multiple authenticated owners, administrators can use the list API
to see all summaries and the event API's `agent` filter to narrow the result.
The single-summary API returns the newest visible owner's summary.

The development seed grants self-only inspection/live permissions to the `agent`
role and all four permissions to `administrator`. Production operators must
provision permission rows and grants explicitly.

PostgreSQL stores `activity_traces` summaries and sanitized `activity_events`.
It stores no payload bodies or secrets. Multiple root requests for one owner's
W3C trace remain running until all roots complete. Child failures make the
completed summary failed; catalog permission filtering alone does not fail an
otherwise successful discovery. Cancellations are distinct from success/failure.
Aborted processes may leave running traces; retention also removes abandoned
traces after their last recent activity ages out.

## Safety, configuration and operations

Central sanitization runs at publication, persistence/live transport and before
OTel export. Metadata is an allowlist of bounded identifiers (`environment`,
`project`, `service`, `agent`, `skill`, `tool`, `connection`) and validated HTTP
method/status values. Arbitrary metadata, bodies, URLs/queries, authorization
headers, provider diagnostics and exception messages are excluded. Sensitive
patterns and configured credential/connection values are redacted even in
identifier fields. Authenticated owner IDs are public routing identities, not
metadata: they remain exact so redaction can never merge two authorization
scopes. Error type is retained with the fixed message
`Operation failed.`; useful failure context comes from the action/entities and
HTTP status, not raw diagnostics. OTel event/link payloads cannot be rewritten
through the public Activity API: spans containing them are explicitly logged and
excluded from export rather than risking disclosure. Application activity events
use the sanitized publisher instead and are unaffected. Do not put business
payloads in activity names or register unreviewed instrumentation.

| Configuration | Default | Meaning |
| --- | --- | --- |
| `ActivityTracing:RetentionDays` | 7 | 0 disables new retained writes; otherwise 1-365 days |
| `ActivityTracing:CleanupIntervalSeconds` | 3600 | Positive worker retention interval |
| `ActivityTracing:QueueCapacity` | 4096 | 16-65536 events buffered per host |
| `ActivityTracing:SubscriberCapacity` | 256 | 1-4096 events per live subscriber |
| `ActivityTracing:OtlpEndpoint` | unset | Optional HTTP(S) OTLP/gRPC collector endpoint, e.g. `http://tempo:4317` |

Use .NET environment names such as `ActivityTracing__RetentionDays=7` on both
hosts. No external collector is registered unless `OtlpEndpoint` is configured.
The Brain works without Tempo/Jaeger/OTLP; use an operator-controlled collector
to route the optional export. Endpoint credentials, query strings and fragments
are rejected; configure collector authentication outside trace metadata.

The bounded dispatch queue decouples request execution from telemetry I/O. It
publishes live notifications independently of retained writes; retention zero
does not disable live delivery or optional OTel export. Storage transient
failures receive bounded, idempotent retries. Exhausted writes, unavailable live
transport and queue overflow are logged explicitly; history can be incomplete.
This is not an audit-grade delivery guarantee or a full telemetry backend.

`Brain.Worker` cleans up expired summaries and cascade-deletes their events on
the configured interval. Recent activity protects an old running trace from
premature removal. Setting retention to zero also allows cleanup of previously
retained history. Audit data is never removed by trace retention.

Apply the checked-in `ActivityTracing` migration before starting either host.
The worker's existing migration readiness gate includes it. API migration and
development seeding remain opt-in as before.

## Verification

`ActivityTests` covers W3C creation/propagation, nested agent/skill/tool/service
spans, failure/cancellation, concurrency, subscriber predicates and central
redaction/export sanitization. `ActivityApiTests` uses real migrated PostgreSQL
for HTTP and MCP correlation, retained aggregation/ownership, authorization,
SSE framing and cross-connection filtering, retention cascades, and actual
loopback HTTP propagation/failure without recording URL/header secrets.

Run the affected checks using the existing runner:

```powershell
dotnet test .\tests\Brain.UnitTests --filter FullyQualifiedName~ActivityTests
dotnet test .\tests\Brain.IntegrationTests --filter FullyQualifiedName~ActivityApiTests
```

Database tests use the normal isolated Testcontainers server, or the
`BRAIN_TEST_POSTGRES` scratch-database mode described in the README.
