# Homelab Brain

A self-hosted shared information platform for independent AI agents. The Brain provides identity, deterministic permissions, scoped memories, canonical knowledge search, reviewed knowledge changes, and skill/tool discovery. It is a modular .NET 10 application, not an agent runtime.

**Milestones 1–7.** Agents submit knowledge proposals; the Brain Writer applies approved changes with revision checks and Git commits. A manifest registry supports permission-filtered skill/tool discovery. HTTP and authenticated MCP share the same application services. Metadata scanning generates reviewed frontmatter proposals, with optional structured semantic enrichment. W3C/OpenTelemetry activity tracing correlates requests, discovery, MCP tools, workers and outbound HTTP, with authorized SSE and retained trace inspection. The API mounts the vault read-only; only the worker receives write access. Both hosts mount `agent-platform` read-only.

## Run locally

Requires the .NET 10 SDK, PowerShell 7, Git, and Docker with Linux containers.

```powershell
./scripts/Initialize-Development.ps1
./scripts/Initialize-DevelopmentVault.ps1
docker compose up --build -d
./scripts/Test-Development.ps1
```

Initialization creates random development credentials in ignored `.env`; it refuses to replace an existing file. The Compose API receives only SHA-256 token digests. Never commit `.env`. The database password is deployment configuration, not an external homelab secret stored in the Brain.

If `.env` already exists from an earlier milestone, keep it and run only the vault initializer before starting Compose.

The vault initializer copies `sample-vault` into ignored `.local/vault`, creates a dedicated Git repository and initial commit, and preserves existing initialized vaults. It never commits changes to the application repository. `BRAIN_VAULT_PATH` may override the Compose mount source. On Linux, set `BRAIN_WRITER_UID`/`BRAIN_WRITER_GID` in `.env` to the owner of the writable vault (the credential initializer sets these for new Linux environments).

The API listens on `http://localhost:5080`, PostgreSQL on `localhost:55432`. Both published ports bind to loopback. PostgreSQL data persists in `brain-postgres`. Stop with `docker compose down`; this preserves that volume.

Compose applies EF migrations and seeds `willem-de-jong` (administrator) and `codex` (agent), both in the `platform` team. The worker removes expired memories and scans the vault/registry every 30 seconds, and processes approved proposals every 5 seconds. Write processing is enabled explicitly in Compose and defaults to disabled in other hosts. API expiry filtering takes effect immediately regardless of worker timing. The limited agent can discover deployment planning, while production deployment and tool dependencies require explicit grants.

Compose also enables deterministic metadata scanning every 300 seconds. A seeded `metadata-enrichment-worker` identity has only knowledge read/propose grants and no credential. It queues pending frontmatter proposals for explicit review; semantic model calls are disabled and metadata always requires explicit review. See [metadata enrichment](docs/metadata-enrichment.md) for schema, issues, configuration and review.

To exercise writes in the development environment, run `./scripts/Test-Development.ps1 -TestKnowledgeWrites`. This explicitly approves a test proposal and leaves one new test Markdown document, proposal/audit records and a Git commit in the development vault. The default smoke test only reads knowledge and tests disposable memory operations.

## Use the API

Load local client credentials without executing `.env` contents:

```powershell
$credentials = @{}
Get-Content .env | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.+)$') { $credentials[$Matches[1]] = $Matches[2] }
}
$headers = @{ 'X-Agent-Id' = 'codex'; Authorization = "Bearer $($credentials['BRAIN_CODEX_TOKEN'])" }
$baseUrl = 'http://localhost:5080'

$memory = Invoke-RestMethod "$baseUrl/api/memories" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    memoryType = 'incident'
    subject = 'Proxmox cluster'
    content = 'Corosync configuration is missing on nacht-02.'
    importance = 0.9
    confidence = 0.95
    visibility = 'private'
    source = 'troubleshooting'
    metadata = @{ host = 'nacht-02' }
} | ConvertTo-Json)

Invoke-RestMethod "$baseUrl/api/memories/$($memory.id)" -Headers $headers
Invoke-RestMethod "$baseUrl/api/memories/search" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    query = 'Proxmox corosync cluster'
    memoryType = 'incident'
    minimumImportance = 0.8
    limit = 5
} | ConvertTo-Json)
```

All data endpoints, readiness and OpenAPI require **both** `X-Agent-Id` and a bearer token bound to that identity. `/health/live` is the sole anonymous process liveness endpoint. Wrong credentials return 401; missing operation permissions return 403; invisible or nonexistent memories both return 404. Unknown JSON fields and integer/unknown enum values return 400.

| Endpoint | Permission / behavior |
| --- | --- |
| `GET /api/agents/me` | Authenticated identity and effective permissions |
| `GET /api/agents` | `agents.read`, up to 1,000 agents |
| `POST /api/agents` | `agents.admin`, register identity and known grants |
| `PUT /api/agents/{id}/access` | `agents.admin`, replace roles/direct grants and enable/disable another agent |
| `POST /api/memories` | `memory.write`; shared visibility also needs `memory.share` |
| `GET /api/memories/{id}` | `memory.read` plus visibility scope |
| `POST /api/memories/search` | `memory.read` plus visibility scope |
| `DELETE /api/memories/{id}` | `memory.delete` and ownership |
| `GET /api/audit?limit=100` | `audit.read`, maximum 1,000 events |
| `GET /api/activity/stream` | `activity.read` + `activity.live`, authenticated SSE; self-only unless `traces.read.all` |
| `GET /api/traces`, `GET /api/traces/{traceId}`, `GET /api/traces/{traceId}/events` | `traces.read`, own traces; `traces.read.all` grants system-wide visibility |
| `POST /api/knowledge/search` | `knowledge.read`, query with optional `pathPrefix` and `limit` |
| `GET /api/knowledge/documents/{id}` | `knowledge.read`, canonical document by indexed ID |
| `GET /api/knowledge/document?path=Infrastructure/Proxmox/nacht-02.md` | `knowledge.read`, canonical document by relative path |
| `POST /api/knowledge/scan` | `knowledge.admin`, immediate scan of the configured vault |
| `GET /api/knowledge/metadata/issues` | `knowledge.read` + `knowledge.admin`, persisted frontmatter issues |
| `POST /api/knowledge/metadata/scan` | Also `knowledge.propose`, generate pending validated metadata patches |
| `POST /api/knowledge/proposals` | `knowledge.read` + `knowledge.propose`, queue a pending change |
| `GET /api/knowledge/proposals` | `knowledge.read`, own proposals; admins can see all; optional `status`/`limit` |
| `GET /api/knowledge/proposals/{id}` | Same proposal visibility rules |
| `GET /api/knowledge/proposals/{id}/preview` | Before/after content and revision compatibility |
| `POST /api/knowledge/proposals/{id}/approve` | `knowledge.read` + `knowledge.admin`, versioned explicit review |
| `POST /api/knowledge/proposals/{id}/reject` | `knowledge.read` + `knowledge.admin`, versioned explicit review |
| `GET /api/skills`, `GET /api/skills/{id}` | `registry.read` plus skill/dependency grants; get includes instructions |
| `POST /api/skills/search` | Permission-filtered skill similarity search |
| `GET /api/tools`, `GET /api/tools/{id}` | `registry.read` plus all tool grants |
| `POST /api/tools/search` | Permission-filtered tool similarity search |
| `POST /api/capabilities/search` | Allowed skills and related allowed tools |
| `POST /api/registry/scan` | `registry.admin`, immediate atomic registry refresh |
| `POST /api/search` | Unified search across granted modules with existing visibility rules |
| `/mcp` | Authenticated Streamable HTTP MCP tools; same bound credentials and permissions |
| `GET /openapi/v1.json` | Authenticated generated OpenAPI document |
| `GET /health/ready` | Authenticated database connectivity check |

Memory types: `observation`, `fact`, `decision`, `task_context`, `incident`, `preference`, `discovery`. Visibility: `private` (default), `team`, `organization`. Importance/confidence range from 0 to 1. `expiresAt` is an optional future ISO 8601 timestamp. Metadata is a JSON object up to 16,384 characters.

Search accepts `query`, optional `agentId`, `memoryType`, case-insensitive subject substring, `minimumImportance`, `minimumConfidence` and `limit` (1–100). Results contain type, ID, title, natural-language content, cosine similarity score, source and structured memory metadata. An agent filter never grants access. Reads/searches increment reference counts and record last access; they do not promote memories.

Register another agent using the administrator credential with `POST /api/agents`:

```json
{"id":"hermes","name":"Hermes","teamId":"platform","roles":["agent"],"permissions":[]}
```

Provision a distinct random bearer token out of band and configure its SHA-256 digest at `Authentication:AgentTokenHashes:hermes`, then restart the API. Registration does not issue a credential. Access updates replace grants; revocations and disabling take effect on the next request. Administrators cannot change their own access through this endpoint.

## Build and test

```powershell
dotnet tool restore
dotnet build HomelabBrain.sln
dotnet test HomelabBrain.sln
```

Unit tests run without a database/network; Git writer tests require the Git CLI and use temporary repositories. By default, integration tests create isolated PostgreSQL 17 + pgvector containers through Docker; they apply real EF migrations and clean up afterward. They never truncate the Compose database. Docker failures fail the tests instead of silently skipping them.

Alternatively, set `BRAIN_TEST_POSTGRES` to an operator-supplied PostgreSQL connection string whose role can create databases and install pgvector. Tests create randomly named `brain_test_*` databases on that server and drop only those databases afterward. No tables in the supplied connection's database are modified. This allows real PostgreSQL testing when Docker is unavailable.

For host-based development, start PostgreSQL with `docker compose up -d postgres`, then set `ConnectionStrings__Brain`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__MigrateOnStartup=true`, `Database__SeedDevelopment=true`, `Knowledge__VaultPath` to the absolute dedicated vault directory, and the `Authentication__AgentTokenHashes__...` variables from `.env`. Run `dotnet run --project src/Brain.Api --urls http://localhost:5080` and the worker separately with the same connection string/vault configuration. Set `Knowledge__WriterEnabled=true` on the worker to process approved proposals. Without a configured vault, memory APIs still work; knowledge calls return 503 and the worker skips indexing.

The EF tooling manifest is `dotnet-tools.json`. Add migrations with:

```powershell
dotnet ef migrations add Name --project src/Brain.Infrastructure --output-dir Migrations
```

## Design and limits

For host-based registry discovery, set `Registry__RootPath` to the absolute `agent-platform` directory in both API and worker. Missing registry configuration disables only that module. Both the sample registry and a separate operator-managed Git checkout are supported. The default smoke test expects the included sample manifests.

Connect an MCP client to `http://localhost:5080/mcp` using Streamable HTTP and the same `X-Agent-Id`/`Authorization` headers. The eight tools cover unified search, memories, canonical reads, pending proposals, skills and capabilities. The default development smoke script now checks MCP too. See [MCP setup, tools and verification](docs/mcp.md); no separate process or port is required.

Tracing is enabled without an external collector. `ActivityTracing__RetentionDays` defaults to 7; the worker removes old retained traces hourly. Setting it to 0 disables retained writes without disabling live events/export. Optional `ActivityTracing__OtlpEndpoint` enables OTLP/gRPC export to an operator-controlled collector. Authenticated operations return `X-Trace-Id` and continue incoming W3C `traceparent`. Development agents can inspect only their own traces; the administrator receives explicit system-wide grants. See [activity tracing](docs/activity-tracing.md) for filters, safety, SSE clients, delivery limits and configuration. No UI animation or external skill execution is included.

See [the foundation design](docs/foundation.md), [knowledge search](docs/knowledge.md), [safe knowledge writes and recovery](docs/knowledge-writes.md), [skills/tools discovery](docs/skills-and-tools.md), [MCP](docs/mcp.md), [metadata enrichment](docs/metadata-enrichment.md), [activity tracing](docs/activity-tracing.md), and [verification](docs/verification.md).

The included `IEmbeddingProvider` uses deterministic 384-dimensional token hashing. It exercises the entire pgvector path without a key, but **is not a learned semantic model**: it primarily finds word overlap and cannot reliably match paraphrases. A real provider and explicit re-embedding are needed before relying on semantic recall. Provider/model/dimension filters prevent incompatible vectors being compared.

Compose is for local development. Production needs operator-provisioned initial identities/roles, external credential configuration, TLS, restricted database access and backups. Automatic development seeding is rejected outside Development. Runtime migrations default to off; apply reviewed migrations as an operator in production. No external secret retrieval is implemented.
