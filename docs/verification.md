# Implementation verification

## PAV OS demo dashboard (18 October 2026)

- Added a separate D3.js + HTML Canvas page without replacing the existing
  dashboard or live Sigma graph. The fictional `ui/demo-vault` contains
  **30 real Markdown files and 84 directed links** across nine folders, with
  `AGENTS.md` as the entry point. The fixture is bundled public example content,
  not a second configured canonical vault, registered capability catalog,
  database memory store or live integration.
- **132 frontend tests** pass across 14 files, including 42 cases across
  five demo-specific files. They verify real fixture discovery, reference
  resolution/reachability, malformed dates/documents, broken/escaping links,
  root-preserving filters, loaded endpoints, six distinct deterministic
  arrangements, finite/non-coincident positions, ring membership, actual
  timeline dates and same-day separation, depth projection, source immutability,
  root-only/narrow layouts, selection/inspection, theme changes, Canvas
  fallback, camera controls, live reduced-motion changes and renderer cleanup.
  The reference-style refinement additionally covers five named capability
  tracks, ring-constrained placement, normalized free placement, temporary
  positions across resize, shared directional edge/particle geometry,
  source-to-target flow progress/wrapping, automatic orbit and guide rotation,
  frame-driven particles, keyboard placement/reset, unnecessary-loop avoidance,
  hidden-page suspension, pointer-release selection without compatibility
  clicks and cancelled-touch handling.
  The existing shell case additionally checks navigation to the new page.
- The TypeScript/Vite production build and Docker UI build pass.
  **Eleven Chromium browser cases** pass against the real Compose UI/API,
  including all existing login, identity, graph, WebObsidian and theme checks.
  Five demo cases verify all six actual Canvas outputs differ, real dot
  selection, linked file/body inspection, filters/no-match states, keyboard
  navigation, camera/rotation controls, reduced motion, color updates, both
  themes, mobile overflow, no-Canvas inspection and absence of graph API calls
  from this demo. Native mouse drags remain selectable on their ring after
  background panning and zooming; free placement, arrow-key movement and reset
  work in Links view. A touch-enabled mobile context uses real touch input to
  drag along the portrait ring and then tap the moved node. Controlled browser
  time verifies changing pixels for directed flow and automatic orbit, stable
  pixels when paused/reduced, and manual rotation with reduced motion.
  No browser exceptions or CSP violations are reported.
- Desktop light/dark and mobile screenshots were inspected. Portrait Ring
  ellipses preserve readable label spacing on narrow screens; a unit case
  asserts at least 14 pixels between labels at a 300-pixel canvas width.
  The rebuilt UI is served by the existing healthy Nginx container.
  No database schema, canonical vault, registry, credentials or backend
  contracts were changed; backend suites were not rerun for this frontend-only
  addition. Existing uncommitted identity work was preserved.

Reproduce against the initialized development stack:

```powershell
npm --prefix .\ui test
npm --prefix .\ui run build
docker compose up -d --build --no-deps ui
npm --prefix .\ui run test:e2e
```

## UI agents and permissions (17 October 2026)

- UI Milestone 4 is implemented on `feat/ui-agents-permissions`. The .NET
  solution builds with zero warnings/errors; the TypeScript/Vite production
  build passes. No database schema, new permission or MCP management operation
  was needed.
- **115 unit/Git cases** and **47 focused HTTP/PostgreSQL cases** pass. The
  integration selection covers agent inspection/management, memory permission
  regression, UI administrator authentication, API contracts and graph access.
  These are focused integration checks, not a full integration-suite claim.
  Nine new identity cases verify direct versus inherited/effective grants,
  unassigned role/catalog inspection, authentication/read checks before lookup,
  live revocation, read-only mutation denial, invalid grants, self-edit
  protection, duplicate creation, disabled identity details/authentication and
  transactional management audits. An identity named `me` is independently
  readable through `/api/agents/{id}/details` without shadowing current identity.
- **90 frontend tests** pass across nine files. New cases cover dedicated
  identity/role matrices, independent lookup and status/search filtering,
  read-only/self-edit states, reviewed registration/full-access replacement,
  correct direct-grant initialization, current-grant refresh/revocation,
  400/401/403/404/409/service/offline failures, explicit empty states, arbitrary
  metadata exclusion, stale response cancellation and direct bounded graph
  focus. Failures do not produce success notices or leak server diagnostics.
- Rebuilt and started the API/UI images against the existing development
  PostgreSQL/worker stack. **Six Chromium cases** pass against the real Nginx
  proxy, API and data, including the new identity case and all prior login,
  theme, graph/WebGL/no-WebGL and WebObsidian checks. Identity verification
  checks actual direct/effective grants, reviews then cancels an enabled-state
  replacement, loads the authorized focused graph, inspects role matrices and
  exercises both themes, mobile overflow and offline navigation recovery.
  No existing agent was changed and no persistent test identity was created by
  the browser case; mutation cases use isolated disposable test databases.
- Documentation and the roadmap describe the supported replacement boundary:
  access updates have no concurrency token and do not merge simultaneous
  operator changes. Credentials, role definitions and permission catalog
  definitions remain operator-managed. No token input/issuance or raw-secret
  management was added.

Reproduce from the repository root:

```powershell
dotnet build HomelabBrain.sln
dotnet test .\tests\Brain.UnitTests
dotnet test .\tests\Brain.IntegrationTests --filter "FullyQualifiedName~AgentApiTests|FullyQualifiedName~MemoryApiTests|FullyQualifiedName~UiAdminApiTests|FullyQualifiedName~ApiContractTests|FullyQualifiedName~GraphApiTests"
npm --prefix .\ui test
npm --prefix .\ui run build
docker compose up -d --build --no-deps api ui
npm --prefix .\ui run test:e2e
```

Use the already-initialized development stack and ignored credentials described
in [the UI guide](ui.md). Integration tests use isolated PostgreSQL 17/pgvector
containers; they do not target the application's database.

## UI WebObsidian integration (17 October 2026)

- UI Milestone 3 is implemented on `feat/webobsidian-integration`. The confirmed
  external document route is `/note/<vault-relative-path>`, using a public
  operator-configured `WebObsidianBaseUrl`. Compose passes the ignored `.env`
  setting through the UI Docker build; native Vite has an equivalent setting.
- **66 frontend tests** pass across eight files; the TypeScript/Vite production
  build passes. Link cases cover absent/invalid configuration, deployment
  prefixes, Unicode/spaces/reserved characters, origin safety, traversal and
  malformed paths, metadata destination rejection, document-only actions,
  denied details and external-link attributes. Existing graph/dashboard/theme
  behavior remains covered.
- The development UI image was rebuilt with the operator-provided base URL.
  **Five Chromium end-to-end cases** pass against the running Compose stack,
  including existing dashboard/login, graph/WebGL/no-WebGL and theme checks.
  The new case selects an actual accessible document through the real graph
  API, verifies its `/note/` target, activates the link with the keyboard and
  checks that the new tab has no opener, authorization, agent-ID or referrer
  headers. Only the external navigation is intercepted. No Brain credentials
  are sent to the external instance, and no external login/document-content
  verification is claimed.
- No backend contract, database schema, canonical write or CSP relaxation was
  needed. The UI remains independent of WebObsidian availability and login.

Reproduce with `npm test`, `npm run build` and `npm run test:e2e` from `ui/`,
after configuring/rebuilding the development UI as described in
[WebObsidian linking](ui.md#webobsidian-linking).

## Graph layout refinement (17 October 2026)

- The graph now spans the available page width without the dashboard width cap.
  Its canvas uses 75% of viewport height on desktop and 70% on small screens,
  with minimum heights of 480/420 pixels respectively and a desktop cap of
  1200 pixels. The inspector is below the canvas, not alongside it.
- Removed the always-visible node list. A compact toolbar selector retains
  keyboard selection and no-WebGL inspection without consuming canvas space.
- Frontend tests and the production build pass. Live Chromium checks assert
  the graph exceeds the old 1540-pixel page cap at a 1920-pixel viewport,
  fills its container, keeps the inspector below it, and preserves mobile width,
  keyboard/no-WebGL access, search, filters and focused exploration.
  The development UI container was rebuilt with this layout.

## UI knowledge graph (17 October 2026)

- UI Milestone 2 is implemented on `feat/knowledge-graph`. The .NET solution
  builds with zero warnings/errors; the TypeScript/Vite production build passes.
- The focused backend regression run passes **156 cases**: 79 unit/Git cases
  covering graph, knowledge, registry-source and metadata behavior, and 77
  HTTP/MCP/PostgreSQL cases covering graph, knowledge, registry, metadata,
  authentication/UI admin and MCP parity. These are focused selections, not a
  claim that the entire solution test suite was rerun. A final 24-case
  graph/knowledge integration rerun also passes after making the graph-reference
  parser a required scanner dependency.
- Eleven graph unit cases cover explicit references, malformed/ambiguous input,
  stable relationship IDs, source grants and traversal work/result bounds.
  Fifteen graph integration cases cover every route, source-grant filtering,
  hidden dependency/permission nodes, live revocation/manifest changes,
  directed/reverse relationships, canonical freshness/deletion, depth 0/1/2,
  invalid limits/types/IDs, empty/invalid sources and bounded queries over 450
  real indexed files. Synthetic private-field/body markers do not reach graph
  responses.
- **28 frontend tests** pass, including graph node/edge construction,
  browser node/edge budgets, server filters/search, independent details,
  expansion/focus/depth, stale response cancellation, explicit failure states,
  no-WebGL handling, renderer disposal and dynamic reduced-motion settings.
- **Four Chromium end-to-end cases** pass against the rebuilt Compose stack,
  without replacing graph API/data with mocks. Graph verification checks actual
  Sigma/WebGL canvases, dependency edges, keyboard selection, detail requests,
  expansion/focus/depth/search/filter requests, both themes, reduced-motion
  operation and mobile width. An aborted graph request verifies offline feedback
  while dashboard navigation remains usable. A separate no-WebGL case verifies
  that the list and inspector still work. Existing login/storage/sign-out and
  theme/CSP browser checks remain passing. Credentials/traces are not recorded.
- The local PostgreSQL 17/pgvector, API, worker and Nginx/UI containers run
  together; the UI health check passes. Visual inspection used a local
  authenticated preview with 38 nodes/48 relationships and identified/fixed
  selected-label contrast in dark mode.
- The generated `KnowledgeGraph` migration adds document reference arrays,
  parser version and a reverse-reference GIN index. Actual test hosts and the
  Compose API apply it; EF reports no pending model changes. Existing source
  data and the dedicated vault are preserved. No graph write API, direct
  canonical edit, WebObsidian link, semantic query or activity overlay was added.

Reproduce with the commands in [the UI guide](ui.md), plus:

```powershell
dotnet test .\tests\Brain.UnitTests --filter "FullyQualifiedName~GraphTests|FullyQualifiedName~KnowledgeTests|FullyQualifiedName~RegistrySourceTests|FullyQualifiedName~MetadataTests"
dotnet test .\tests\Brain.IntegrationTests --filter "FullyQualifiedName~GraphApiTests|FullyQualifiedName~KnowledgeApiTests|FullyQualifiedName~RegistryApiTests|FullyQualifiedName~MetadataApiTests|FullyQualifiedName~McpTests|FullyQualifiedName~UiAdminApiTests|FullyQualifiedName~ApiContractTests"
```

## UI admin login and Docker development stack (17 October 2026)

- The complete Compose environment was built and run using PostgreSQL 17 with
  pgvector, the real API and worker, and the production-built React UI served by
  Nginx. The UI is available at `http://localhost:5173`; no browser API mocks or
  native-database fallback were used for this check.
- Five frontend tests and the TypeScript/Vite production build pass. The
  dependency audit reports zero vulnerabilities.
- One Chromium end-to-end test passes against the running Compose stack:
  invalid tokens are rejected, token-only administrator login resolves the
  configured identity, all eight dashboard requests return 200, local/session
  storage remain empty, and sign-out returns to an empty token input. The test
  also checks for browser page errors. Credential recording/tracing is disabled.
- Forty focused backend cases pass, covering `ApiContractTests`,
  `UiAdminApiTests`, memory authentication/visibility, and MCP regression tests.
  Real PostgreSQL verifies that revoking grants and disabling the mapped
  administrator take effect on the next request.
- The hosted development smoke script passes for authenticated memory
  round trips, pgvector search, current canonical knowledge reads,
  permission-filtered registry discovery, OpenAPI, and MCP initialization,
  eight-tool discovery and unified search. Disposable smoke-test memories were
  deleted; the reviewed canonical-write opt-in was not run.
- Container verification exposed shell entrypoint removal of credential
  environment keys containing hyphenated agent IDs. Direct apphost execution
  preserves those settings, and the legacy administrator/agent smoke checks
  pass alongside the new UI token flow.
- Re-running UI credential initialization preserves existing credentials.
  `docker compose config --quiet` and `git diff --check` pass.

Reproduce with `scripts\Start-Development.ps1`, then `npm run test:e2e` from
`ui/` (install Chromium once with `npx playwright install chromium`). See
[the UI guide](ui.md) for credential provisioning, token-only authentication,
and the remaining dashboard API gaps. This does not implement the graph,
connection manager, or later UI milestones.

## Current result — Milestone 7 (17 October 2026)

- The solution builds with zero warnings/errors. The full regression suite passes:
  **199 tests, zero failed/skipped** (104 unit/Git tests and 95 integration tests).
- Database verification used an isolated native PostgreSQL **16.15** instance
  with pgvector **0.8.6**, installed under ignored task-local artifacts. Tests
  created/dropped only their own randomly named scratch databases. Docker was
  unavailable; this is not a Docker/Compose deployment verification.
- The new `ActivityTracing` migration was applied by the actual HTTP test hosts.
  EF reports no pending model changes; whitespace checks pass.
- Ten tracing unit tests cover W3C context creation/propagation, nested
  agent/skill/tool/service spans, cancellation/failure, concurrent contexts,
  filtering, ownership-safe redaction and actual exporter suppression of
  unsafe immutable OTel event payloads.
- Eighteen tracing integration cases cover sampled/unsampled incoming context,
  concurrent HTTP traces, skill and dependency graph references, MCP parentage,
  child-failure aggregation, shared trace IDs across owners, sensitive-pattern
  owner IDs, explicit permissions and live grant revocation, SSE framing and
  filters across independent database connections, disabled retention, bounded
  slow subscribers, query filters and retention cascades. The real retention
  background service and worker failure wrapper run against the database.
  Native loopback HTTP verifies outbound W3C propagation and failure correlation
  without exposing authorization headers or URL query secrets.
- Existing HTTP/MCP, memory, knowledge, registry, metadata and reviewed Git writer
  regression cases remain passing. Trace data remains separate from audit data.
  No UI animation, skill executor, deployment or live LLM call was introduced.
- External OTLP collector deployment was not exercised. Optional export uses
  the standard OTel exporter; sanitization/export filtering is checked locally.
  Default operation and all acceptance tests require no external telemetry
  collector.

See [activity tracing](activity-tracing.md) for APIs, permissions, configuration,
delivery limits and reproduction commands.

## Previous verification — Milestones 1–6

- `dotnet build HomelabBrain.sln`: passed, zero warnings/errors.
- Automated suites: **171 passed, zero failed/skipped** (94 unit/Git tests; 77 HTTP/MCP/database/Git integration tests). The full regression run passed, followed by focused reruns for metadata rendering, lowercase proposal-status binding and related proposal/MCP behavior.
- Actual database verification used an isolated native PostgreSQL **16.15** instance with pgvector **0.8.6**, installed under ignored `artifacts/native-tests` from conda-forge. Docker remains the default integration path (PostgreSQL 17); the native run is not a claim that the Docker images were built or run.
- Integration tests created randomly named databases on the native instance and dropped only their own databases. `BRAIN_TEST_POSTGRES` selects this mode; normal operation still uses Testcontainers when that variable is unset.
- A real API process and a separate Worker process passed the development smoke test against another temporary database. The worker also detected a newly added Markdown file and re-indexed a later edit without an administrative scan. Temporary probe file/processes/database were cleaned up.
- The Milestone 3 hosted smoke test used a separate temporary Git vault and database. The API queued a pending proposal, returned a review preview, accepted an explicit versioned approval, and the real worker applied it. The test verified one new Git commit, proposer identity, matching canonical content and a clean checkout. Temporary vault/database/processes were cleaned up.
- The Milestone 4 hosted smoke test started API and Worker concurrently against a fresh database. The worker indexed the sample registry without an administrative scan. HTTP discovery returned planning for the limited agent, excluded production deployment/protected tools, and returned the production skill with all three dependencies for the administrator. Skill instructions, authenticated OpenAPI, memory/knowledge operations and a reviewed Git write also passed. Its temporary database, vault and processes were cleaned up.
- The fresh-start test exposed a PostgreSQL type-cache race when the worker connected before API migrations installed pgvector. The worker now waits for migrations and the extension, refreshes type metadata, and then starts background jobs. A separate integration test reproduces distinct API/worker connection pools on a new database and verifies a vector write after readiness.
- The Milestone 5 hosted smoke test verified actual HTTP/SSE framing at `/mcp`: initialization, all eight tool definitions, structured results, private memory creation/search, canonical document/skill reads, filtered discovery and unified search. A separate MCP create-document proposal remained pending and no canonical file appeared. The existing reviewed HTTP proposal still produced exactly one Git commit with a clean checkout. Temporary memories, database, vault and processes were cleaned up.
- Official MCP SDK client tests exercise protocol revisions `2025-11-25` and `2026-07-28` against the real ASP.NET app and PostgreSQL. MCP and HTTP use the same scoped services and bound identity. No database migration was needed for Milestone 5.
- Milestone 6 metadata tests cover schema validation, human-field/comment/body preservation, Unicode/CRLF, invalid/ambiguous YAML, deterministic values, malformed or invalid semantic JSON, optional HTTP provider formatting, full/body hash separation, unchanged/rejected cache skips, content/schema/model refresh, scanner identity/grants, atomic state/proposal/audit persistence, scan exclusion, changes during provider I/O and stale approved conflicts. Metadata proposals use the existing preview/review/Git writer.
- The Milestone 6 hosted smoke test enabled the real metadata worker against a temporary vault/database. It queued pending proposals before any administrative metadata scan without changing canonical files. Explicit HTTP review then applied one sample metadata patch with matching preview, one additional Git commit and a clean checkout. Memory, knowledge, registry, MCP and ordinary reviewed writes also passed in the same API/worker run. All temporary resources were cleaned up.
- Semantic model calls remained disabled in hosted tests; provider transport/response validation used a fake HTTP handler/provider. No live LLM endpoint or paid API was contacted. A real optional endpoint is operator-configured; live model quality/availability is not established by these tests.
- `docker compose config --quiet`: passed. The API mounts the dedicated development vault read-only; the worker has write access and explicit writer enablement. The vault initializer created ignored `.local/vault` from samples with an initial commit, without committing application repository changes.
- `dotnet ef migrations has-pending-model-changes --project src/Brain.Infrastructure --no-build`: passed.
- `git diff --check`: passed (Git reports its normal LF/CRLF conversion notice).
- Docker container build/run remains unverified because Docker Desktop has not recovered (latest status: `starting`). No global prune, volume deletion, forced termination of Docker processes or WSL reset was used. The API and worker binaries were verified as native processes.

Milestones 1–6 are implemented and tested. Metadata generation is review-only; confidence never approves a write. The deterministic development embedding provider remains a mock, so these tests establish vector plumbing/ranking and access controls, not learned semantic recall.

## Reproduce checks

Run `dotnet build HomelabBrain.sln` and `dotnet test HomelabBrain.sln`. With no `BRAIN_TEST_POSTGRES`, tests fail if Docker cannot start the isolated integration database. To use an existing pgvector-enabled PostgreSQL server, set `BRAIN_TEST_POSTGRES` to a connection whose role can create databases/install the vector extension. Tests never truncate tables in that connection's original database.

Unit tests cover authenticated ownership, default privacy, score/enum validation, missing permissions, sharing requirements, vector validation, normalization and repeatability.

Knowledge unit tests cover heading/fence/frontmatter chunking, line references, oversized lines and Unicode boundaries, stable/path-bound/content-bound tokens, UTF-8 BOM/invalid encoding, oversized/missing files, prohibited paths, linked roots, directory junction escapes and cycles. Fixture cleanup removes links independently of their targets.

Writer unit tests use real Git repositories and verify section boundaries/ambiguous headings, stale tokens, exact-once append/replay, branch-publication recovery before/after materialization, newer-content preservation, BOM/CRLF, literal filenames, staged-change preservation, junction rejection, hook suppression, prepared-object retention through Git garbage collection, retry after a Git reference lock failure, and hidden uncommitted edits. Windows device paths and exact path casing are also rejected by the vault policy.

Integration tests use the actual HTTP app, EF migration, PostgreSQL 17 and pgvector. They cover bound credentials, role/direct grants/revocation, memory round trips, cosine ranking, visibility, filters before limits, expiry/cleanup, deletion/cascades, model isolation, denial audits, authenticated OpenAPI, invalid input, idempotent seed, model drift and transaction rollback.

Knowledge integration tests cover canonical reads, vector sections/citations, permission enforcement, indexing/skipping/modification/deletion, stale hit suppression, model/chunker changes, concurrent scan leases, edits during embedding, unavailable vault preservation and rollback of failed chunk replacements. Database tests use real migrations and cosine operations. The default Docker run targets PostgreSQL 17; the completed native fallback run used PostgreSQL 16 as noted above.

Proposal HTTP/PostgreSQL/Git tests cover pending/review/preview/application separation, all three operations, stale approved proposals, competing proposals, creation collisions/deletion, proposal ownership, permissions/revocation, concurrent reviews, rejection/invalid requests, deferred dirty checkouts, writer exclusion, crash replay from a published prepared commit, and database/row-version failures after Git success. Row-version races abort rather than overwriting a concurrent row's status.

Registry tests cover strict YAML/schema validation, folded descriptions, IDs and permissions, aliases/tags/duplicate fields, linked directories, file bounds/UTF-8, instruction hashing, skill/tool ranking, source metadata, all required tool grants, role/direct grant unions and revocation, missing dependencies, permission filtering before limits, stale source/dependency/model suppression, source changes during search/indexing, unchanged scans, deletion, malformed source preserving the old index, transactional replacement rollback, invalid embedding output, scan leases, operation authorization and OpenAPI. Worker readiness is tested against a fresh scratch database with separate connection pools before and after pgvector migration.

MCP tests cover both protocol generations, eight-tool/schema discovery, structured and text results, authenticated ownership, private/shared memory visibility across clients, HTTP parity, canonical reads/revision tokens, pending proposals and later reviewed Git application, stale-token conflicts, skill/tool permission filtering, unified search, optional-source failures, permission revocation without reconnecting, disabled/mismatched identities, Origin rejection, absence of session IDs, invalid enum/score/path/limit arguments, and rejection of caller-supplied identity/approval arguments without persistence.

For metadata, use `scripts/Test-Metadata.ps1` after development setup to scan and inspect generated pending proposals. Add `-ApplySampleMetadata` only to explicitly approve the sample metadata patch; that flag leaves frontmatter and a Git commit in the development vault. The normal metadata scan never applies proposals. See [the schema and workflow](metadata-enrichment.md).

For Compose, initialize credentials with `scripts/Initialize-Development.ps1` if needed, initialize the dedicated checkout with `scripts/Initialize-DevelopmentVault.ps1`, run `docker compose up --build -d`, then `scripts/Test-Development.ps1`. Add `-TestKnowledgeWrites` to exercise a reviewed write; it deliberately leaves a test Markdown document, proposal/audit records and a knowledge Git commit in that development vault. The default test only reads knowledge and removes its temporary memory.

## Initial Docker failure history — 16 October 2026

- The full solution build passed with zero warnings/errors, including the final OpenAPI and validation changes.
- All 11 unit tests and 5 HTTP contract tests passed. The HTTP contract tests validate binding, authenticated identity, OpenAPI security schemes and anonymous liveness without replacing the PostgreSQL acceptance tests.
- The first PostgreSQL integration attempt exposed test-host configuration timing; database configuration resolution was corrected.
- Subsequent integration execution was blocked by Docker Desktop filesystem I/O errors and failure to mount its WSL data disk. C: had approximately 1.7 MB free. `dotnet clean` recovered roughly 74 MB from this task's outputs.
- After disk space was freed (approximately 13 GB available), Docker reported `starting` and still returned server errors. PostgreSQL tests were retried and failed on Docker connectivity.
- `docker desktop restart --timeout 60` failed with `context deadline exceeded` because Docker processes remained running. The engine did not recover. No global prune, volume deletion or WSL reset was performed.
- Compose configuration validation and EF pending-model-change checks passed. Random ignored local credentials were initialized in `.env`.
- At that point PostgreSQL acceptance and Compose build/smoke validation were not confirmed. The native PostgreSQL and hosted-process runs above subsequently resolved the functional acceptance gap.

The native database run exposed and fixed a Development-mode HTTP binding error: invalid enum JSON produced 500 rather than 400 when ASP.NET threw `BadHttpRequestException`. The middleware now returns the request error status consistently, and the real HTTP/database suite passes.
