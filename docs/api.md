# API and authentication

The API listens on `http://localhost:5080` in local development. See [setup](setup.md) for the full stack and [MCP](mcp.md) for the Streamable HTTP MCP surface.

## Authentication

Agent and MCP clients require both `X-Agent-Id` and a bearer token bound to that identity. REST, readiness, and OpenAPI also accept the dedicated UI administrator bearer token without an agent ID when explicitly configured.

`Authentication:UiAdminTokenHash` stores the UI token's SHA-256 digest and `Authentication:UiAdminAgentId` selects the existing enabled identity whose current database permissions apply. Compose maps it to `willem-de-jong`; `BRAIN_UI_ADMIN_ID` can override that mapping. The UI digest must differ from agent digests. Disabling the identity or revoking its grants takes effect on the next request. MCP does not accept the UI token.

`/health/live` is the sole anonymous endpoint. Wrong credentials return 401; missing operation permissions return 403; invisible and nonexistent memories both return 404. Unknown JSON fields and invalid integer or enum values return 400.

## Example request

Load local credentials without executing `.env` contents:

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
```

## Endpoint catalog

| Endpoint | Permission / behavior |
| --- | --- |
| `GET /api/agents/me` | Authenticated identity and effective permissions |
| `GET /api/graph`, `GET /api/graph/search` | Bounded, source-permission-filtered graph overview/search; up to 200 entities and 400 relationships |
| `GET /api/graph/entity?id=...`, `GET /api/graph/neighbors?id=...` | Entity details and progressive expansion; server depth 0–2 with source visibility checks |
| `GET /api/agents` | `agents.read`, up to 1,000 agents |
| `GET /api/agents/{id}/details` | `agents.read`, direct and effective grants |
| `GET /api/roles`, `GET /api/permissions` | `agents.read`, role grants and permission catalog |
| `POST /api/agents` | `agents.admin`, register an identity and known grants |
| `PUT /api/agents/{id}/access` | `agents.admin`, replace another agent's roles/grants and enabled state |
| `POST /api/memories` | `memory.write`; shared visibility also needs `memory.share` |
| `GET /api/memories/{id}` | `memory.read` plus visibility scope |
| `POST /api/memories/search` | `memory.read` plus visibility scope |
| `DELETE /api/memories/{id}` | `memory.delete` and ownership |
| `GET /api/audit?limit=100` | `audit.read`, maximum 1,000 events |
| `GET /api/activity/stream` | `activity.read` + `activity.live`, authenticated SSE; self-only unless `traces.read.all` |
| `GET /api/traces`, `GET /api/traces/{traceId}`, `GET /api/traces/{traceId}/events` | `traces.read`; own traces unless `traces.read.all` |
| `POST /api/knowledge/search` | `knowledge.read`, optional `pathPrefix` and `limit` |
| `GET /api/knowledge/documents/{id}` | `knowledge.read`, indexed canonical document |
| `GET /api/knowledge/document?path=...` | `knowledge.read`, canonical document by relative path |
| `POST /api/knowledge/scan` | `knowledge.admin`, immediate vault scan |
| `GET /api/knowledge/metadata/issues` | `knowledge.read` + `knowledge.admin`, persisted frontmatter issues |
| `POST /api/knowledge/metadata/scan` | `knowledge.read` + `knowledge.propose`, generate validated metadata proposals |
| `POST /api/knowledge/proposals` | `knowledge.read` + `knowledge.propose`, queue a pending change |
| `GET /api/knowledge/proposals` | `knowledge.read`, own proposals; admins can see all |
| `GET /api/knowledge/proposals/{id}` | Same proposal visibility rules |
| `GET /api/knowledge/proposals/{id}/preview` | Before/after content and revision compatibility |
| `POST /api/knowledge/proposals/{id}/approve` | `knowledge.read` + `knowledge.admin`, explicit versioned review |
| `POST /api/knowledge/proposals/{id}/reject` | `knowledge.read` + `knowledge.admin`, explicit versioned review |
| `GET /api/skills`, `GET /api/skills/{id}` | `registry.read` plus skill/dependency grants |
| `POST /api/skills/search` | Permission-filtered skill similarity search |
| `GET /api/tools`, `GET /api/tools/{id}` | `registry.read` plus tool grants |
| `POST /api/tools/search` | Permission-filtered tool similarity search |
| `POST /api/capabilities/search` | Allowed skills and related tools |
| `POST /api/registry/scan` | `registry.admin`, immediate atomic registry refresh |
| `POST /api/search` | Unified search across granted modules |
| `/mcp` | Authenticated Streamable HTTP MCP tools |
| `GET /openapi/v1.json` | Authenticated generated OpenAPI document |
| `GET /health/ready` | Authenticated database connectivity check |

Memory types are `observation`, `fact`, `decision`, `task_context`, `incident`, `preference`, and `discovery`. Visibility is `private` by default, with `team` and `organization` scopes. Importance and confidence range from 0 to 1. Search supports query, agent, type, subject, importance, confidence, and a 1–100 result limit. Reads and searches update reference telemetry; they do not promote memories.

Registering an agent does not issue a credential. Provision a distinct bearer token out of band and configure its SHA-256 digest under `Authentication:AgentTokenHashes:<id>`. Access updates replace grants, and administrators cannot change their own access.
