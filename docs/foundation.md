# Foundation design — Milestone 1

## Scope and plan

The initial repository contained only a starter README. Build a working identity-to-memory vertical slice: domain/contracts, PostgreSQL mappings and migration, deterministic access, HTTP authentication, memory storage/vector search, audit, expiry worker, Docker environment, unit tests and PostgreSQL integration tests. Do not begin Milestone 2.

## Structure and boundaries

```text
HomelabBrain.sln
src/
  Brain.Domain/          Identity, permission constants, memory and audit entities
  Brain.Application/     Contracts, validation and memory use cases
  Brain.Infrastructure/  EF persistence, identity/grants, vectors, mock embeddings
  Brain.Api/             HTTP authentication, endpoints, OpenAPI, composition root
  Brain.Worker/          Expired-memory cleanup host
tests/
  Brain.UnitTests/
  Brain.IntegrationTests/
docker/                 Shared API/worker Dockerfile
scripts/                Local credentials and smoke test
sample-vault/           Template copied into the dedicated .local/vault development repository
docs/
```

Dependencies point inward: Domain has no packages; Application references Domain; Infrastructure implements Application interfaces; API and Worker compose them. The worker is a background host, not a separately owned data service. Both hosts use the same database and contracts.

The eventual `Brain.Mcp` will be another transport calling Application services. Milestone 2 adds [knowledge search](knowledge.md); Milestone 3 adds [reviewed proposals and the Git writer](knowledge-writes.md). Skills, tools, policies and prompts can later live in `agent-platform/`.

## Initial PostgreSQL schema

| Table | Main columns and relationships |
| --- | --- |
| `agents` | String ID, name, description, nullable team ID, enabled, JSONB metadata, created/updated timestamps |
| `roles` | String ID, description |
| `permissions` | Exact permission ID, description |
| `agent_roles` | Composite key `(agent_id, role_id)`, foreign keys |
| `role_permissions` | Composite key `(permission_id, role_id)`, foreign keys |
| `agent_permissions` | Direct grants, composite key `(agent_id, permission_id)` |
| `memories` | UUID, owner FK, type, subject, natural-language content, importance, confidence, visibility, team snapshot, timestamps, expiry, source, JSONB metadata, reference count, last access, promotion status |
| `memory_embeddings` | Memory UUID PK/FK, `vector(384)`, provider, model/version identity, dimensions |
| `audit_events` | UUID, actor ID, action, resource, timestamp, result, JSONB metadata |

The one-to-one embedding table keeps pgvector types outside Domain. Memory deletion cascades to embeddings; agent deletion is restricted while memories reference it. Audit actor IDs deliberately have no FK so events survive identity lifecycle changes and can identify `brain-worker`.

Database checks constrain scores, memory types, visibility/team requirements, promotion statuses and dimensions. JSONB holds flexible annotations; grants, ownership and lifecycle fields use typed columns. Timestamps use `timestamptz` and UTC. The checked-in EF migration creates the vector extension, requiring database-owner/extension installation privileges.

Indexes cover ownership, expiry, visibility/team scope, embedding model and per-agent audit time. Initial vector ranking is exact cosine search. An approximate HNSW index is deferred until volume and permission-filtered recall can be measured. Scope and metadata filters precede ranking/limit in SQL.

## Identity and permissions

Requests present `X-Agent-Id` and `Authorization: Bearer <token>`. Each configured agent digest must be a distinct valid SHA-256 digest. Generate tokens from at least 32 random bytes. Hash comparison is constant-time. Credentials/digests are not stored in PostgreSQL or returned by APIs. Supplying an ID or another agent's credential cannot impersonate that agent. Disabled/unregistered agents fail authentication.

The server reloads roles, role permissions and direct grants on each authenticated request. Effective permissions are their exact, case-sensitive union. There are no wildcards, caller-supplied roles, AI decisions or implicit administrator bypass of memory privacy.

The development `administrator` role receives `agents.read`, `agents.admin`, `audit.read`, and all memory permissions. The `agent` role receives `memory.read`, `memory.write`, `memory.delete`, `memory.share`. Registration accepts only known role/permission IDs. Grant changes are administrative and audited. Role definitions are seeded in development; general role CRUD and production provisioning are outside this vertical slice.

Memory ownership comes from the authenticated context. Private content is visible only to its owner. Team content is visible to the owner and current agents in the stored team. Organization content is visible to agents with `memory.read` in this single homelab organization. Sharing needs `memory.share`; deletion needs ownership plus `memory.delete`. Agent filters intersect with visibility. Expired data is hidden before cleanup.

Team assignment is administrative data. Team-visible memories retain their originating team ID. If a future feature moves an owner to another team, the original team retains access unless an explicit migration changes sharing. Multi-organization tenancy and explicit team membership tables are future requirements.

Only process liveness is anonymous. Readiness and OpenAPI require identity. OpenAPI declares the ID header and bearer scheme together. API inputs reject unknown fields and use snake_case string enums. No endpoint exposes SQL, filesystem access, external secrets or vault writes.

## Embeddings

Application owns `IEmbeddingProvider`, whose descriptor identifies provider, model/version and dimensions. The service validates finite, nonzero vectors with the expected dimensions. Infrastructure maps them to pgvector and stores the descriptor. Search compares only matching descriptors. Generation is synchronous in the first slice; provider failure prevents persistence.

The development provider uses normalized, stable SHA-256 token feature hashing without network/API keys. It is a mock for vector plumbing, not a learned semantic model. Real providers can implement the contract. Changing dimensions requires a schema migration; changing models requires explicit re-embedding. Vectors must never silently be relabeled. Until re-indexed, mismatched model memories remain readable by ID but are excluded from vector search.

## Audit and worker

Memory creation stores the memory, vector and audit atomically. Owned deletion and expiry cleanup also transact with audit. Reads/searches record resource access without copying content or queries into audit; they increment reference counts and last access atomically. Access-counter updates and read audit are separate commits because they are telemetry. Authenticated permission denials are audited; invisible and missing IDs both return 404.

The worker removes expired memories, indexes canonical knowledge and registry metadata, scans frontmatter for pending enrichment proposals, and applies explicitly reviewed knowledge changes. Jobs retry after failures and honor cancellation. Remote Git synchronization and automatic memory promotion are not implemented. Importance, confidence, reference count, last access and promotion status support future consolidation.

## Architectural concerns

- Mock embedding retrieval quality is limited; evaluate a real provider before relying on semantic recall.
- Fixed vector dimensions are an explicit tradeoff; provider changes require migrations/re-embedding.
- Static per-agent credentials suit an initial homelab slice. Central identity, rotation without restart, rate limits and richer credential lifecycles can follow. TLS is required beyond loopback.
- Local PostgreSQL runs as schema owner. Production should separate migration/runtime privileges, protect backups, restrict network access and provision initial identities/roles outside development seeding.
- Infisical remains external; no external secret values or retrieval are implemented. Arbitrary natural-language memories are not automatically screened for accidental secrets; clients must not submit credentials.
- `agents.admin` can grant permissions to other identities and is highly privileged. Memory privacy is checked independently.
- Audit retention/export and pagination beyond bounded lists remain follow-up work.
- The foundation was extended by Milestone 2 with read-only vault access/path validation/revision tokens, Milestone 3 with reviewed proposals and the dedicated Git writer, [Milestone 4](skills-and-tools.md) with manifest-based discovery, [Milestone 5](mcp.md) with authenticated MCP tools, and [Milestone 6](metadata-enrichment.md) with reviewed frontmatter enrichment.
