# Skills, tools, and capability discovery

Milestone 4 adds an operator-managed registry, pgvector discovery, and deterministic
authorization. A skill contains instructions; a tool describes an external
integration. Brain does not execute either, retrieve secrets, or connect to the
listed systems. [Milestone 5 MCP transport](mcp.md) exposes these discovery services.

## Canonical files and index

`Registry:RootPath` points to an `agent-platform` checkout. Compose mounts the
included sample directory read-only in both API and worker. A separate Git
repository works with the same layout:

```text
agent-platform/
  skills/<id>/manifest.yaml
  skills/<id>/SKILL.md
  tools/<id>/manifest.yaml
```

Skill manifest:

```yaml
id: deploy-service
name: Deploy Service
description: >
  Deploy an application to the homelab production environment.
tags: [deployment, production]
required_tools: [gitlab, coolify, infisical]
required_permissions: [deployment.production]
```

Tool manifest:

```yaml
id: coolify
name: Coolify
description: Manage applications and deployments.
provider: mcp
tags: [deployment]
permissions: [deployment.read, deployment.write]
capabilities: [deploy_service, restart_service, get_deployment_status]
```

All displayed fields except `tags` are required; use explicit empty lists where
there are no requirements. Tool providers are `mcp` or `http`. IDs must match
their directory and contain lowercase letters, digits, and hyphens, starting with
a letter or digit (maximum 100 characters). Windows device names are prohibited.
Permission names are exact lowercase names such as `deployment.production`;
there are no wildcard grants. Capability names also allow underscores.

The parser accepts one YAML mapping with text and text-list fields. It rejects
unknown/duplicate keys, duplicate list values, nested values, tags, anchors,
aliases, multiple documents, and invalid schema values. Files must be UTF-8.
Manifests are limited to 16 KiB, nonempty skill instructions to 64 KiB, and the
catalog to 500 definitions. Links in root ancestors, definition directories, or
files are prohibited. Definition paths are constructed from validated directory
IDs; callers cannot supply filesystem paths. Keep credentials and secret values
outside manifests and instructions.

`RegistryDefinition` represents source metadata and optional skill instructions
in the domain. `RegistryService` owns authorization, discovery, and indexing in
Application; HTTP calls this service, as a future MCP adapter will.
`FileSystemRegistrySource` and `RegistryIndex` implement the filesystem and
PostgreSQL boundaries in Infrastructure.

The `registry_entries` table stores typed metadata, PostgreSQL text arrays for
tags/permissions/dependencies/capabilities, source hashes, embedding model identity,
and a `vector(384)`. Skill instructions remain canonical files and are not stored
in the index. Descriptions, names, tags, and tool capability names feed the shared
`IEmbeddingProvider`; the included token-hash provider exercises similarity
ranking but does not provide learned semantic recall.

The worker scans immediately and then every `Worker:RegistryScanIntervalSeconds`
(default 60; Compose 30). A PostgreSQL advisory lease excludes concurrent scans.
Before starting any jobs, the worker waits for all application migrations and
pgvector to be present, then refreshes the PostgreSQL type cache. This also handles
starting the worker before the API creates the schema on a fresh database. The
worker does not apply migrations itself.
An unchanged catalog with the same model is skipped. A changed catalog is bounded
and re-embedded as a whole, checked again for source changes, and replaced in one
database transaction with an audit event. A failed scan preserves the previous
index. Deleted entries disappear on the next successful replacement. Provider,
model, and dimensions must match for searching; dimensions currently must be 384.

Discovery reads live canonical manifests. If any source is malformed or
unavailable, the request fails closed (422 for invalid definitions, 503 for missing
or unreadable source) until it is repaired. Search excludes stale or deleted
entries immediately, including skills whose tool index is stale. It rechecks
hashes after embedding/search to catch changes while a query is in flight.
Hashes include both the skill manifest and instruction text. List/get operations
read and authorize current canonical definitions without requiring an index;
search requires a current index. Registry updates should be published by the
operator as a consistent checkout, outside agent access.

## Authorization

Every discovery/read operation requires `registry.read`; administrative scan
requires `registry.admin`. Identity is taken from authenticated request context,
never from a discovery request body. Existing database role and direct grants
are unioned on each request; revocations apply to the next request.

For every tool, **all** manifest permissions are required. For every skill, all
its own permissions and every required tool's permissions are required. Missing
tools deny the skill. Neither administrator role names nor registry administration
bypass these checks. Manifest files describe requirements; they never grant
permissions or create identities/roles. Unknown permission names consequently
deny access unless explicitly provisioned as grants by an operator.

Filtering happens before the database result limit. There is no LLM authorization
or approval decision. Unauthorized entries are omitted; direct reads return the
same 404 as nonexistent entries. Capability access produces
`brain.capability.discovered` audits; excluded definitions produce
`brain.capability.denied` audits with the agent and resource. Admin denied-result
diagnostics are not exposed in the response.

Development seeding grants `registry.read` to the `agent` role, plus its existing
memory and knowledge access. Thus `codex` can discover/read `plan-deployment`, but
cannot use `deploy-service` or its protected tools. The development administrator
receives the seeded deployment/infrastructure/secret-reference permissions and
can discover all configured sample dependencies. No actual secret is seeded.
Production identities and grants remain operator-provisioned; seeding is disabled
outside Development. Tool requirements are deliberately conservative: split
read-only and write-capable tool definitions if different grants are needed.

## HTTP API

All routes use the existing agent ID header and bound bearer credential.

| Route | Purpose |
| --- | --- |
| `GET /api/skills` | List allowed canonical skill metadata |
| `GET /api/skills/{id}` | Read allowed skill metadata and full `instructions` |
| `POST /api/skills/search` | Rank allowed, current indexed skills |
| `GET /api/tools` | List allowed canonical tool metadata |
| `GET /api/tools/{id}` | Read allowed tool metadata |
| `POST /api/tools/search` | Rank allowed, current indexed tools |
| `POST /api/capabilities/search` | Discover skills and tools together |
| `POST /api/registry/scan` | Administer an immediate index refresh |

Search body:

```json
{"query":"I need to deploy an application","tags":["deployment"],"limit":10}
```

`query` is required (1–4,000 characters), `tags` is optional (all specified tags
must match), and `limit` is 1–100, default 10. Tags match exactly. Individual
searches return an array; capability search returns `{ "skills": [], "tools": [] }`.
Entries contain `type`, `id`, `title`, description as `content`, cosine `score`,
relative manifest `source`, and typed `metadata` with requirements and source hash.
Lists/direct reads have a null score. Skill reads wrap the entry in `skill` and
include `instructions` separately.

Capability search ranks up to `limit` of each kind and includes **all** tools
required by returned skills, even if those tools do not match the query tags or
rank in the top tool results. Consequently the tools array can exceed `limit`;
dependency-only results have a null score. Authorization and freshness apply to
every dependency. No similarity threshold is imposed; a low score means weak
textual relevance, not permission to execute an external action. The caller's
harness must configure and authorize its integrations independently.

For host-based development, set `Registry__RootPath` to the absolute
`agent-platform` directory in both processes. Without it, registry requests return
503 and the worker skips registry indexing. Other modules still work.

`scripts/Test-Development.ps1` scans the sample registry, discovers planning for
the limited agent, verifies production skill/tool filtering, and verifies the
administrator can discover the production workflow. Integration tests also cover
revocation, missing/stale dependencies, live source changes, malformed manifests,
model isolation, concurrent scans, and atomic failure recovery.
