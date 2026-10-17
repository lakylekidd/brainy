# Conventions

These conventions capture the current code, build settings and documented
contracts of Homelab Brain. Build-enforced settings are identified below; the
remaining guidance preserves existing patterns rather than claiming a formatter
or policy that the repository does not configure. For system boundaries, read
[architecture](architecture.md).

## Toolchain and local workflow

[`global.json`](../global.json) requests .NET SDK 10.0.102 with `latestFeature`
roll-forward. [`Directory.Build.props`](../Directory.Build.props) applies
`net10.0`, implicit usings, nullable reference types and warnings-as-errors across
the solution. Package versions are explicit in each project; `dotnet-tools.json`
pins the EF tool. Local scripts require PowerShell 7; Git tests require Git.
The normal integration environment requires Docker with Linux containers.

Run commands from the repository root:

```powershell
dotnet tool restore
dotnet build HomelabBrain.sln
dotnet test HomelabBrain.sln
```

For a local Compose environment:

```powershell
.\scripts\Initialize-Development.ps1
.\scripts\Initialize-DevelopmentVault.ps1
docker compose up --build -d
.\scripts\Test-Development.ps1
```

Initialize credentials only when `.env` does not exist; the script intentionally
refuses to replace them. The vault initializer preserves an existing dedicated
Git vault and refuses to overwrite a non-repository directory. Do not use the
application checkout as a writable knowledge vault.

There is currently no checked-in `.editorconfig`, dedicated formatter/linter
configuration, or CI pipeline. Do not describe formatting preferences or a
locally successful command as enforced CI.

## Placement and dependency rules

| Change | Existing home / pattern |
| --- | --- |
| Entities, lifecycle values and permission IDs | `Brain.Domain`; keep EF, pgvector and transport packages out |
| Use cases, request/result records and dependency interfaces | `Brain.Application`; reference Domain, not Infrastructure or either host |
| Database, filesystem, Git and provider adapters | `Brain.Infrastructure`; implement Application boundaries |
| HTTP routes, bound authentication and OpenAPI | `Brain.Api`; delegate reusable use cases |
| MCP schemas and tool wrappers | `Brain.Mcp`; reuse the same services and `ICurrentAgent` |
| Scheduled execution | `Brain.Worker`; create a service scope per job iteration |

Feature contracts are grouped in files such as `KnowledgeContracts.cs`,
`KnowledgeProposalContracts.cs` and `MetadataContracts.cs`. Follow the nearest
feature's organization instead of introducing a new parallel framework.

Register shared services in `ServiceRegistration.AddBrain`. Keep
`AddKnowledgeWriter` separate and worker-only in production hosting; tests can
register it explicitly to exercise publication. Stateless vault/chunker/options
services are singletons; database stores and application services are generally
scoped. Never capture scoped stores or a `DbContext` in a singleton or use one
context concurrently.

## C# style

Existing code uses four-space indentation, file-scoped namespaces matching
projects, PascalCase types/members, camelCase parameters/locals and `I`-prefixed
interfaces. Follow the surrounding file rather than reformatting unrelated code.

Use records for request/result snapshots and mutable classes for persisted
entities and configuration. Sealed classes, primary-constructor injection,
collection expressions, pattern matching and target-typed construction are
common. Short delegating methods may be expression-bodied. Comments should
explain non-obvious invariants, concurrency or recovery boundaries.

Asynchronous I/O methods generally use the `Async` suffix and accept a trailing
`CancellationToken` (`ct` or `cancellationToken`). Public use cases commonly
default it; adapters propagate it to I/O. Prefer injected `TimeProvider` for
application/worker time and UTC `DateTimeOffset` values. Do not swallow shutdown
cancellation as a failed operation.

## Names and wire formats

Do not apply one naming policy indiscriminately across all boundaries.

| Surface | Convention / example |
| --- | --- |
| C# types, properties and enum members | PascalCase: `MemoryType`, `CreatedAt`, `ReplaceSection` |
| HTTP JSON properties and MCP arguments/results | camelCase: `memoryType`, `revisionToken`, `skillId` |
| HTTP/MCP enum strings | snake_case: `task_context`, `replace_section`; integer and unknown values are rejected |
| Metadata patch/provider JSON and frontmatter fields | snake_case: `schema_version`, `related_concepts`, `metadata_version` |
| PostgreSQL tables and columns | snake_case: `knowledge_write_proposals`, `created_at` |
| Permission IDs | Exact lowercase dot-separated names: `knowledge.propose`, `deployment.production` |
| Registry IDs and directory names | Lowercase letters/digits/hyphens: `deploy-service`; match manifest `id` |
| MCP tool names | Explicit `brain_` snake_case names: `brain_read_document` |
| Audit actions | Existing `brain.<area>.<event>` names, such as `brain.knowledge.applied` |
| Feature guide filenames | Lowercase, hyphenated Markdown names |

HTTP JSON rejects unknown members. MCP also rejects unknown root-level tool
arguments and uses case-sensitive property binding. Keep strict contracts when
adding fields or tools; do not silently accept caller-supplied identity, grants
or approval flags.

## Identity, validation and errors

Take actor identity from authenticated `AgentContext`, never from a body/tool
argument. Use `Permissions` constants and `AgentContext.Require`/`Has` for
operation checks. Grants are an exact union of roles and direct permissions:
no wildcard matching, role-name shortcuts or model-based authorization.
Provision new permission rows/grants explicitly, extending development seeds
where appropriate without implying production provisioning.

Keep reusable validation and authorization in services, not just transport
wrappers. Use `Validation.Require`, `Text`, `Score` and `Metadata` where their
semantics fit. `Validation.Text` trims its return value; proposal Markdown
intentionally validates without replacing the original text. Preserve meaningful
whitespace, BOM and newline content in canonical changes.

Preserve established access invariants: private memory is the default, sharing
needs `memory.share`, deletion needs ownership, expiry hides content before
cleanup, and query filters never expand visibility. All required skill/tool
grants and dependencies must pass before discovery ranking/limits.

Controlled errors use `BrainException` and its status:

| Status | Established meaning |
| --- | --- |
| 400 | Invalid input or unsupported operation |
| 401 | Missing, invalid, mismatched or disabled bound credentials; handled by authentication |
| 403 | Authenticated operation lacks required access |
| 404 | Missing or invisible resource; do not reveal private resource existence |
| 409 | Revision/review conflict or optimistic concurrency failure |
| 413 | Canonical document/result exceeds its byte limit |
| 422 | Invalid source, inconsistent read or invalid provider response |
| 503 | Unconfigured/unavailable source or deferred repository condition |

HTTP maps controlled errors to Problem Details. MCP maps them to controlled
`Brain <status>: <message>` tool errors; transport authentication remains HTTP.
Do not return provider diagnostics, credential values or unexpected exception
details to clients.

Catch expected failures narrowly. Unified search reports known optional-source
failures explicitly rather than pretending they are no matches; unexpected
database/provider errors still fail the operation. Worker retry boundaries log
failures, and batch operations report failures/conflicts/deferred state.

## Persistence, concurrency and audit

Keep EF mappings in `BrainDbContext` and generated migrations/model snapshots in
`Brain.Infrastructure\Migrations`. Use typed columns, lengths, checks,
relationships and indexes for constrained data; JSONB is for flexible metadata,
not a substitute for explicit grants or lifecycle fields.

Generate migrations with the existing EF tool rather than editing snapshots by
hand:

```powershell
dotnet ef migrations add DescriptiveName --project .\src\Brain.Infrastructure --output-dir Migrations
dotnet ef migrations has-pending-model-changes --project .\src\Brain.Infrastructure
```

Check in the migration, its generated metadata and the model snapshot together.
Development may opt into API startup migrations/seeding; production applies
reviewed migrations explicitly. The worker waits for schema readiness.

Persist a state change and its audit together where the existing store operation
provides that boundary. Explicit transactions are needed for multi-step database
updates. Read counters/read audits are telemetry and can be separate commits.
Use resource IDs/paths, actors, outcomes and bounded metadata in audit events;
do not copy queries, memory/document/proposal bodies or credentials into audit.

Keep visibility, expiry, model and candidate filtering before SQL result limits.
Embedding descriptors must match provider, model and dimensions; vectors must
have the expected dimension, finite components and a nonzero value. Never
silently relabel existing embeddings. Current vector columns are fixed at 384.

Use the existing PostgreSQL advisory lease pattern for cross-process jobs and
release connections/leases reliably. Review uses the proposal `Version` token;
preserve `ProposalConcurrencyException` instead of overwriting another review.
Database transactions cannot make Git and file publication atomic: retain the
prepared-intent/replay design described in [knowledge writes](knowledge-writes.md).

## Canonical files, registry and metadata

Client knowledge paths use `/` even on Windows. They must be relative portable
Markdown paths with exact casing, not host filesystem paths. Reject traversal,
links/reparse points, device names and invalid UTF-8; retain configured byte
limits. Hash raw file bytes for canonical revisions, including BOM/newline
changes. Revision tokens identify snapshots; they are not credentials.

Do not serve cached chunks as current canonical documents, prune after an
incomplete scan, or return stale search hits. Recheck source revisions after
embedding/provider work. Changes belong in pending proposals; only the dedicated
writer publishes explicitly approved results. Keep Git calls shell-free with
argument lists and literal paths, and preserve unrelated work on conflict.

Registry layout is `skills/<id>/manifest.yaml` plus `SKILL.md`, and
`tools/<id>/manifest.yaml`. Use the strict existing YAML/schema parser; requirements
in manifests do not grant access. Registry files stay operator-managed and
read-only to both hosts. See [skills/tools](skills-and-tools.md) for required
fields, limits and supported providers.

Metadata patches use structured, validated JSON, not model-generated YAML.
Preserve valid human fields, comments, unknown fields and the Markdown body.
Do not auto-approve based on confidence or accept generic caller-created
`metadata_enrichment` proposals. Update the runtime policy and
[default schema](schemas/frontmatter-v1.schema.json) together when changing
defaults; keep both hosts' policies compatible. See
[metadata enrichment](metadata-enrichment.md) for preservation and cache rules.

## Configuration and secrets

Use standard .NET `Section:Key` configuration names and `Section__Key` environment
variables. Distinguish application defaults from Compose overrides when
documenting settings. Do not add hidden fallback databases, enable production
seeding, or enable writes/semantic calls implicitly.

Keep `.env`, local vaults and generated artifacts ignored. Store only configured
token digests on the API side; provision raw credentials out of band. No secret
values belong in memories, registry manifests, skill instructions, audit events
or committed examples. Semantic enrichment is an explicit opt-in that sends
vault excerpts to the configured endpoint; use an appropriately trusted provider.
Brain does not retrieve external homelab secrets.

## Tests and documentation

Use xUnit `[Fact]`/`[Theory]`, behavior-describing underscore-separated test names,
and existing fixtures/helpers. Unit tests use small fakes for application
boundaries; filesystem and writer tests use real temporary files/Git repositories
through `VaultSandbox` and `TestGit`.

Integration tests use the actual ASP.NET app, EF migrations and PostgreSQL with
pgvector via `BrainFixture`; MCP tests use the official SDK client. Default
database tests start isolated PostgreSQL 17 containers. Alternatively,
`BRAIN_TEST_POSTGRES` selects a server on which tests create/drop randomly named
scratch databases. Never point destructive fixture cleanup at an application
database. The database-free `ApiContractTests` do not replace database acceptance
tests, and unavailable Docker is a failure, not a silent skip.

For focused work, run the affected project or combine related test classes in a
runner filter before escalating to the full solution. For example:

```powershell
dotnet test .\tests\Brain.UnitTests
dotnet test .\tests\Brain.IntegrationTests --filter "FullyQualifiedName~KnowledgeProposalApiTests|FullyQualifiedName~MetadataApiTests"
```

Cover denied access, invalid input and source changes as well as success paths.
Changes to writes/indexes should exercise conflicts, rollback/replay, stale
sources and preserved canonical bytes. HTTP/MCP changes need contract parity
where both expose the use case. Provider tests use fakes, not paid/live calls.

Smoke checks have deliberate side effects: the default development script
creates/deletes temporary memories and scans sources; `-TestKnowledgeWrites`
leaves an applied document and vault commit. MCP `-TestKnowledgeProposal` leaves
a pending proposal; metadata `-ApplySampleMetadata` approves a sample patch and
leaves a vault commit. Do not treat these opt-in write checks as read-only.

Keep the README as the setup/entry-point guide and `docs` as linked architecture,
convention and feature references. Update directly affected guides, examples and
schemas alongside behavior changes. Distinguish implemented behavior, defaults,
development overrides and deferred work; do not present historical
[verification](verification.md) results as a newly executed run.
