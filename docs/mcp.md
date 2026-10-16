# Brain MCP server

Milestone 5 exposes Brain through Streamable HTTP at `/mcp` in the existing API
process. `Brain.Mcp` is a transport adapter referencing `Brain.Application`; it
has no database or filesystem implementation. The host supplies the same scoped
identity and application services as HTTP. No new database migration is needed.

The implementation pins the official
[MCP C# SDK 2.2.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.2.0).
It uses the SDK's stateless HTTP mode, typed tool schemas, protocol negotiation,
structured results, cancellation, and error handling. Tests exercise both
`2025-11-25` initialization and `2026-07-28` discovery with an official SDK client.
See the [SDK transport options](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol.AspNetCore/HttpServerTransportOptions.cs)
and [tool documentation](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/docs/concepts/tools/tools.md).

## Connect an agent

Start the existing Compose stack as described in the main README. No separate MCP
container or port is needed. Configure a client's **Streamable HTTP** MCP connection
with these settings, using its secret/environment configuration rather than
committing a token into a project file:

| Setting | Development value |
| --- | --- |
| URL | `http://localhost:5080/mcp` |
| `X-Agent-Id` header | `codex`, or another provisioned agent ID |
| `Authorization` header | `Bearer <that agent's raw client token>` |

Use a distinct ID and credential for each agent/harness. The initialized ignored
`.env` contains the development client tokens. The server receives their SHA-256
digests only. Registration and role/direct grant management remain the HTTP admin
operations; clients cannot choose an identity in a tool argument. A harness needs
Streamable HTTP and configurable authentication headers. Browser-only or
OAuth-only connection flows are not provided in this milestone.

Every request, including initialization/discovery and `tools/list`, is
authenticated. Permissions and enabled status are resolved from the database on
each HTTP request, so changing access takes effect without reconnecting. Wrong
credentials, unknown agents and disabled agents receive HTTP 401. No session ID,
cookie, or earlier initialization can grant access to a later call. Calls with an
`Origin` header receive 403; this endpoint is intended for native/server clients
and does not enable browser cross-origin access.

The endpoint uses no persistent MCP sessions, legacy SSE endpoint, server-side
sampling, elicitation, resource subscriptions, or direct filesystem resources.
Individual POST responses may use SSE framing, which Streamable HTTP clients
handle. Production uses the same endpoint behind operator-configured HTTPS;
preserve both credential headers and MCP transport headers through the proxy.

## Tools

Tool arguments are flat, use camelCase, and omit agent identity. Enum arguments
are snake_case strings. Every successful tool returns both structured content and
a JSON text content block for clients that display text.

| Tool | Arguments and result | Authorization |
| --- | --- | --- |
| `brain_search` | `query`, optional `limit`; knowledge/memories/skills/tools/failures arrays | Searches only granted modules |
| `brain_remember` | `subject`, `content`; optional memory fields; returns memory | `memory.write`, plus `memory.share` for shared visibility |
| `brain_search_memories` | `query`, optional memory filters; returns `{memories: [...]}` | `memory.read` plus existing visibility scope |
| `brain_read_document` | `path`; canonical content and `revisionToken` | `knowledge.read` |
| `brain_propose_knowledge_change` | `path`, `operation`, `content`, `reason`; optional `revisionToken`/`section`; returns pending proposal | `knowledge.read` + `knowledge.propose` |
| `brain_find_skills` | `query`, optional `tags`/`limit`; returns `{skills: [...]}` | `registry.read` plus all skill/dependency grants |
| `brain_get_skill` | `skillId`; returns metadata and canonical instructions | Same skill/dependency checks |
| `brain_find_capabilities` | `query`, optional `tags`/`limit`; returns skills and related tools | Same registry checks for every result |

`brain_remember` accepts `memoryType` (default `observation`), `visibility` (default
`private`), `importance`/`confidence` (default 0.5), optional `expiresAt`, `source`
and object `metadata`. Types, sizes, scores, expiry, and sharing follow the HTTP
contract. Do not store secret values in memories.

`brain_search_memories` accepts optional `memoryType`, subject substring,
`minimumImportance`, `minimumConfidence`, and `limit`. Every memory operation uses
the existing ownership/team/organization checks; administrator role names do not
expose another agent's private memories. Search filters never extend visibility.

Search limits are 1–100, default 10. Registry tag filters require every exact tag.
Capability results include all authorized dependencies of returned skills, so
their tools array can exceed `limit`. Registry results remain discovery metadata;
Brain does not invoke those external systems or retrieve Infisical secret values.

`brain_read_document` accepts only vault-relative Markdown paths under the
existing traversal/link/device-name restrictions. Knowledge proposals support
`append`, `replace_section`, and `create_document`. Append/section changes need
the revision token from a prior canonical read. Section replacement also needs
the exact heading hierarchy in `section`; new documents need an unused path and
no revision token. Every proposal starts pending, and MCP has no approval or
writer tool. Review remains explicit through the existing administrative HTTP
flow; only the worker applies approved changes with revision checks and Git
commits. Repeated write calls are not idempotent and can create additional rows.

Metadata enrichment introduced in [Milestone 6](metadata-enrichment.md) uses the
administrative HTTP scan/review flow. Although the shared operation enum includes
`metadata_enrichment`, generic MCP/HTTP proposal submission rejects it. The
scanner must generate and validate those patches. The eight MCP tools stay the
same.

## Unified search

`SearchService` in Application implements both `brain_search` and
`POST /api/search`, with body `{"query":"corosync recovery","limit":10}`.
It searches memory only with `memory.read`, knowledge only with `knowledge.read`,
and the registry only with `registry.read`, then returns the four source arrays.
Each underlying service still enforces its detailed authorization rules.
An agent without any search permission receives a denial.

The limit applies independently to each source; there is no global cross-source
score normalization or ranking. Registry results include skill dependencies as
above. Unavailable or invalid optional knowledge/registry sources return empty
arrays and an explicit `failures` entry with `source`, `status`, and `detail`;
other authorized sources can still return results. Ungranted sources are not
queried. Unexpected database/provider errors fail the operation instead of being
silently reported as no matches. The development embeddings are still a token
hash mock; these APIs do not imply learned semantic recall.

## Errors and audit

Known application errors produce `isError: true` with controlled text such as
`Brain 403: The agent lacks the required permission.` or a 409 revision conflict.
Unknown extra tool arguments produce a JSON-RPC invalid-parameters error. Invalid
enum/scalar types, missing arguments, and application validation failures cannot
persist a memory/proposal. The SDK hides unexpected internal exception details.
Transport authentication errors remain HTTP 401; browser Origin rejection is 403.

Core tool definitions can be listed by authenticated clients regardless of grants;
listing a Brain operation grants no authority to call it. Discovered external
skill/tool capabilities are filtered by the registry service. Memory, knowledge,
proposal and registry audits use the same services as HTTP; MCP permission
denials add `brain.authorization.denied` with the tool name and calling agent.

## Verify

```powershell
./scripts/Test-Development.ps1
./scripts/Test-Mcp.ps1
```

The development smoke test now includes MCP initialization, eight-tool discovery,
private memory creation/search, canonical and skill reads, filtered capability
discovery, and unified search. It removes its temporary memory through HTTP.
It expects the sample vault and registry to be indexed, so run the full
development test first on a fresh stack.

To exercise the proposal tool explicitly:

```powershell
./scripts/Test-Mcp.ps1 -TestKnowledgeProposal
```

This leaves a pending test proposal and audit records, with no canonical Markdown
change or Git commit. It deliberately does not approve the change. Existing
`-TestKnowledgeWrites` on the development script separately tests reviewed writes.

Integration tests use the official `McpClient` against the actual ASP.NET
application and PostgreSQL/pgvector. They cover both protocol generations, all
eight tools, structured schemas/results, HTTP parity, concurrent-agent isolation,
role revocation, disabled identities, unknown arguments, Origin enforcement,
optional-source failure reporting, path validation, and the existing reviewed
Git writer flow after an MCP proposal. No client configuration on the user's
machine is changed by these tests.
