# Brain UI roadmap

This roadmap preserves the intended UI milestones from the original Brain UI
brief. It is a planning document, not a commitment to implement every proposed
feature unchanged: confirm each milestone's API contracts, safety boundaries,
and scope before starting it. Follow [the UI guide](ui.md) for implemented
behavior and current API coverage.

## Product boundaries

- Brain is an operations and exploration surface, not a Markdown editor.
  Canonical knowledge remains in Markdown and Git; use WebObsidian to create
  and edit knowledge documents.
- Authorization is enforced by the backend. Hiding an action or filtering a
  graph in the browser never grants or revokes access.
- Secrets remain in Infisical. The UI may manage references, but must not
  retrieve or render raw secret values.
- Derive graph entities and deterministic relationships from canonical Brain
  data. Do not introduce a second source of truth or require manual graph
  maintenance for ordinary Brain operations.
- Treat AI-suggested metadata or relationships as proposals for human review,
  never as authorization or automatic writes.
- Design graph loading for large data sets: server-side filtering, bounded
  subgraphs, depth limits, and progressive expansion. Do not load every entity
  into the browser by default.
- Render explicit, allowlisted fields from API responses; never dump arbitrary
  registry, audit, trace, or event metadata. Preserve clear loading, empty,
  error, offline, unauthorized, and conflict states.
- Support desktop and responsive layouts, keyboard access, and reduced motion.
  Graph exploration may be desktop-first but should remain usable on smaller
  screens.

## Status

| Milestone | Status | Current position |
| --- | --- | --- |
| 1. Shell + Dashboard | Complete | Authenticated shell, API-backed dashboard, responsive layout, and system-aware light/dark themes are implemented. |
| 2. Knowledge Graph | Complete | Stable source-derived entities/edges, bounded authorized graph APIs, Sigma/Graphology exploration, search/filters, independent details, focus and progressive expansion are implemented. |
| 3. WebObsidian Integration | Complete | Document inspection links canonical source paths to an operator-configured WebObsidian `/note/<path>` route, with safe encoding and explicit unavailable configuration states. |
| 4. Agents + Permissions | Partial foundations | Existing APIs feed dashboard data; agents, roles and grants can be explored in the generic graph. Dedicated agent, role, and permission-management pages are not implemented. |
| 5. Skills + Tools | Partial foundations | Registry APIs feed the dashboard and the generic graph shows authorized dependencies. Dedicated lists/detail and safe Git-backed management remain unimplemented. |
| 6. Connections | Planned | No connection catalog, configuration, health, or test UI/API is established. |
| 7. Proposals + Metadata | Partial foundations | The dashboard reads pending proposals and metadata issues. Review diffs, conflict handling, approval, and a dedicated review experience remain to be built against the safe writer workflow. |
| 8. Operations + Audit | Partial foundations | The dashboard shows bounded recent audit and trace activity. Job/sync diagnostics and a full audit experience are not implemented. |
| 9. Semantic Graph | Planned | Knowledge semantic search and deterministic graph exploration exist, but there is no graph semantic-query or temporary semantic-subgraph API. |
| 10. Live Activity Graph | Planned; graph and backend foundations exist | Milestone 2 provides the static renderer; Brain Milestone 7 provides activity tracing and retained trace inspection. Confirm an authorized live stream and graph-entity correlation contract before implementation. |

“Partial foundations” means only the stated data or backend foundations exist;
it does not mean the corresponding management UI is complete. See [the UI
guide](ui.md#existing-api-coverage-and-gaps) for current dashboard endpoints and
known gaps.

## Milestone 2 — Knowledge Graph

**Status: Complete (17 October 2026).** The implementation derives graph views
from current sources rather than maintaining another canonical graph catalog.
Documents cache only explicit reference IDs in the existing versioned index.
Four authenticated graph routes enforce existing source permissions, a maximum
of 200 entities/400 relationships and depth 0-2. The isolated graph feature adds
global/search/focused views, independent allowlisted details, filters and
progressive expansion, with keyboard/no-WebGL access and explicit error states.
The graph uses the full available width and a viewport-scaled canvas, with the
inspector below it and a compact accessible selector instead of a node list.
See [usage and contracts](ui.md#knowledge-graph) and
[verification evidence](verification.md#ui-knowledge-graph-17-october-2026).
WebObsidian links, semantic subgraphs and live overlays remain out of scope.

**Goal:** Let users explore Brain entities and their relationships in an
interactive, bounded graph.

**Scope**

- Define generic, stable-ID graph entities and relationships with extensible
  types, metadata, and references to their canonical source records.
- Derive document entities from indexed knowledge documents, skills/tools from
  operator-managed manifests, and agents/roles from Brain data. Generate
  deterministic edges from manifest dependencies, permissions, and explicit
  metadata references. Keep graph data synchronized with its sources.
- Add authenticated, permission-filtered APIs for a bounded global graph,
  entity details, neighbors/relationships, and graph search. Establish explicit
  result and relationship-depth limits; do not expose hidden entities or
  metadata.
- Integrate Sigma.js and Graphology in an isolated graph feature.
- Implement global and focused graph views, node selection/details, filters,
  search, and progressive neighbor expansion.
- Keep canonical document bodies and skill instruction bodies out of graph
  payloads.

**Acceptance criteria**

- A user can search, filter, select, and progressively explore accessible Brain
  entities and relationships.
- The selected entity is inspectable without loading the entire graph.
- Relationship depth and result bounds are enforced by the server.
- Unauthorized entities, edges, and metadata never appear, including through
  search, expansion, or detail requests.
- Empty, loading, error, and unauthorized states leave the rest of the
  application usable.
- Tests cover node/edge rendering, filters, focus mode, depth bounds, search,
  access control, and large-graph loading behavior.

## Milestone 3 — WebObsidian Integration

**Status: Complete (17 October 2026).** The operator confirmed the document
route from an existing instance URL: `/note/<vault-relative-path>`.
The document inspector uses the authorized canonical source path and an
operator-controlled public `WebObsidianBaseUrl` build setting. Compose/native
Vite wiring, independent segment encoding, new-tab credential/referrer
isolation and explicit missing/invalid configuration states are implemented.
Frontend unit/build and real-backend Chromium checks pass; external navigation
is intercepted rather than signing into WebObsidian. See
[configuration and boundaries](ui.md#webobsidian-linking) and
[verification evidence](verification.md#ui-webobsidian-integration-17-october-2026).
No canonical editing, vault synchronization or later management UI was added.

**Goal:** Open the canonical Markdown document represented by a graph node in
WebObsidian.

**Scope**

- Show document-specific node details and an **Open in WebObsidian** action.
- Add an operator-configured `WebObsidianBaseUrl` and use the documented
  WebObsidian document route.
- Construct the target from the canonical vault-relative path, encoding path
  segments safely. Do not allow document metadata to choose an arbitrary
  destination origin.

**Acceptance criteria**

- Selecting an accessible document node opens the matching canonical document
  in the configured WebObsidian instance.
- Invalid or absent URL configuration is handled explicitly; no unsafe,
  metadata-controlled external link is generated.
- Tests cover URL configuration, path encoding, and origin safety.

## Milestone 4 — Agents + Permissions

**Goal:** Inspect and manage agent identities, roles, and effective permissions.

**Scope**

- Add agent listing and details, role inspection, a permission matrix, and
  effective-permission views.
- Add an agent-focused graph view when Milestone 2 is available.
- First verify which read and mutation contracts already exist. Add any
  missing backend contracts before wiring management actions; keep
  authorization and validation server-side.

**Acceptance criteria**

- The user can inspect an agent's roles and effective access.
- Any management action is exposed only where a backend operation with
  deterministic authorization exists.
- A focused graph view shows only entities and relationships that the current
  identity is authorized to see.
- Tests cover role/permission display, relevant permission cases, and
  unauthorized actions.

## Milestone 5 — Skills + Tools

**Goal:** Inspect the registry and, only after agreeing a safe write contract,
manage skill/tool definitions without losing Git as their source of truth.

**Scope**

- Add skills and tools lists, detail views, tool capabilities, and dependency
  visualization; add focused graph views when Milestone 2 is available.
- Determine the supported management boundary for operator-managed registry
  files before implementing edits. Do not invent direct writes from the UI or
  bypass repository validation, review, or Git history.
- If a safe backend workflow is approved and available, provide Git-backed
  editing and synchronization/status feedback through that contract.

**Acceptance criteria**

- Users can inspect skills, tools, capabilities, and dependencies with the
  same access filtering as the backend.
- Any edit uses an explicitly supported, validated, recoverable Git-backed
  workflow, and the canonical source remains the operator-managed registry.
- Tests cover registry filtering, detail rendering, dependency links, and all
  authorized/unauthorized write paths that are implemented.

## Milestone 6 — Connections

**Goal:** Provide a safe control-plane view for external Brain integrations.

**Scope**

- Add connection listing and non-secret configuration, secret references,
  health status, and connection testing.
- Establish backend contracts for connection ownership, permission checks,
  health, and test results before building interactive management.
- Store references to Infisical secrets only; never store or return raw
  credentials in Brain data or the UI.

**Acceptance criteria**

- An authorized operator can configure an integration using secret references
  without exposing the secret value.
- Connection health and test outcomes are explicit and do not imply success
  when unavailable.
- Tests verify validation and permissions, redact secrets in every response and
  rendered state, and cover health/test failures.

## Milestone 7 — Proposals + Metadata

**Goal:** Review and approve knowledge and metadata changes using Brain's
existing proposal/revision-token/Brain Writer workflow.

**Scope**

- Add dedicated knowledge proposal and metadata issue/review views.
- Show safe before/after diffs, proposal context, validation results, and
  revision conflicts.
- Use existing preview, review, approval, and writer APIs where available;
  identify any missing API contract before implementation.
- Do not edit canonical files in the browser or silently overwrite a newer
  revision. Approval remains explicit and backend-authorized.

**Acceptance criteria**

- An authorized reviewer can inspect a proposal and its diff, distinguish
  conflicts, and explicitly approve or reject it using supported backend
  operations.
- A stale revision is surfaced and never silently overwritten.
- Metadata enrichment remains review-only; confidence or model output cannot
  approve a write.
- Tests cover diffs, approval/rejection permissions, stale conflicts, metadata
  proposals, and secret-safe field rendering.

## Milestone 8 — Operations + Audit

**Goal:** Make background processing, registry synchronization, and audit
activity diagnosable.

**Scope**

- Add job health, last/next run, sync status, failure details, and supported
  sync-trigger actions.
- Add a searchable/filterable audit log. Reuse the authenticated audit API and
  retain backend visibility rules.
- Establish job and synchronization status/trigger contracts before presenting
  controls; the current API does not provide complete job or skill-sync health.

**Acceptance criteria**

- Operators can distinguish healthy, running, failing, and unavailable status
  without fabricated defaults.
- Supported synchronization actions report their actual result and require
  backend authorization.
- Audit filtering and pagination remain permission-scoped.
- Tests cover audit filters, job failure/unavailable states, sync outcomes, and
  unauthorized actions.

## Milestone 9 — Semantic Graph

**Goal:** Turn a natural-language query into a temporary, contextual graph of
relevant entities and known relationships.

**Scope**

- Define an authorized graph semantic-query API and bounded semantic-subgraph
  response. Existing knowledge semantic search is not itself a graph API.
- Show query, results, and supporting known relationships in the graph without
  changing the canonical graph.
- Clearly distinguish deterministic known relationships from any model
  suggestion. Relationship suggestions, if later supported, are proposals and
  require human review.

**Acceptance criteria**

- A query such as “show me everything related to production deployment” returns
  relevant authorized entities and their known relationships as a bounded,
  temporary subgraph.
- Search cannot reveal inaccessible entities, edges, or metadata.
- Empty, unavailable, and provider-error outcomes are explicit; no
  success-shaped fallback or unreviewed canonical relationship write occurs.
- Tests cover query handling, bounded output, authorization, and graph rendering.

## Milestone 10 — Live Activity Graph

**Goal:** Add a transient activity overlay to the existing knowledge graph,
correlating live Brain activity with known graph entities.

**Prerequisites**

- Milestone 2 graph visualization and bounded expansion are implemented.
- Brain Milestone 7 activity tracing/live events are functional. Verify the
  actual authenticated stream protocol, event schema, and source/target entity
  correlation; do not assume that retained traces alone provide a live stream.

**Scope**

- Reuse the existing graph renderer. Live events temporarily animate known
  graph nodes/edges; they never mutate canonical graph data.
- Consume the authorized live stream with disconnect/reconnect handling,
  interruption and duplicate/replay handling. Keep static graph interaction
  available when the stream is offline.
- Distinguish running, succeeded, failed, and cancelled activity; retain
  concurrent trace identity and provide an activity panel, trace selection,
  ordered detail timeline, filters, and optional follow mode (off by default).
- Add on/off and other useful live-activity preferences. Use bounded history,
  event batching, throttled updates, and expiry cleanup; do not redraw the
  entire graph per event.
- Support Focus Graph and semantic subgraphs without loading the entire graph
  to follow an event.
- Make activity status understandable without relying on color or animation,
  and respect reduced-motion preferences.
- Render allowlisted trace/event fields only. Never expose secrets or hidden
  entity data.

**Acceptance criteria**

- The UI connects to the authorized activity stream and shows reconnecting
  state without disabling the static graph.
- Activity between already-loaded entities is visually indicated with distinct
  running, success, failure, and cancellation outcomes; transient state expires
  without changing the durable graph.
- Concurrent traces remain distinct. Selecting one highlights its path and
  exposes its details and ordered operation/duration timeline.
- Users can filter and disable live updates; disabled mode leaves the static
  graph fully functional.
- Focus and semantic graph views handle out-of-view activity through bounded,
  user-directed expansion.
- High event frequency is batched and does not cause full-graph rerenders;
  in-memory activity remains bounded.
- Reduced-motion, reconnect, duplicate/malformed event, authorization, and
  secret-safe rendering behaviors are tested.
- Recent completed traces use retained Brain trace APIs, never reconstructed
  browser-side event history.

## Suggested delivery order

The numbered milestones are the original sequence. Milestones 4, 5, 7, and 8
can reuse existing backend foundations, but their dedicated UI and any missing
backend contracts are still incomplete. Milestone 3 depends on document nodes
from Milestone 2. Milestone 9 depends on graph entities and bounded graph
responses. Milestone 10 depends on both the graph implementation and the
functional activity stream; it must extend, not replace, the graph.
