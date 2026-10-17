# Brain UI

The Brain UI is an operations and exploration surface, not a Markdown editor.
WebObsidian remains the place to create and edit canonical knowledge documents;
the Brain UI must link out to it rather than copy editing functionality.

## Current scope: UI Milestones 1-4

The frontend is a React + TypeScript + Vite application under `ui/`. Its current
structure is:

```text
ui/src/
  api/client.ts
  components/
  features/dashboard/
  features/graph/
  features/agents/
  features/pav/
  App.tsx
  styles.css
```

The shell exposes the dashboard, knowledge graph, agents, permissions and a
separate PAV OS demo dashboard,
including configured WebObsidian links for document nodes; later milestone routes are
explicit placeholders. The dashboard reads existing authenticated API endpoints
for agent, skill, tool, pending-proposal, metadata-issue, audit, trace, and
readiness data. Counts reflect the API response visible to the current identity;
the UI does not fabricate counts for missing data. The current API has no
dedicated totals for knowledge documents or memories and no connection catalog
or job-status endpoint, so those metrics are identified as unavailable.

The status of later UI milestones, their scope, acceptance criteria,
dependencies, and safety constraints are tracked in the
[Brain UI roadmap](ui-roadmap.md).

Run the complete development stack with `scripts\Start-Development.ps1`.
Compose builds the UI and serves it on loopback port 5173 through Nginx,
which proxies `/api` and `/health` to the API container. Its health check
requires both the static UI and the anonymous API liveness route to respond.
The database uses its existing persistent volume; the API applies development
migrations/seeds, and the worker waits for database readiness before indexing.
Nginx re-resolves the API service name, including after an API container restart.
No new backend or Aspire host is introduced.

For hot reload, stop the Compose UI container and run the frontend from `ui/`
with `npm ci` and `npm run dev`. Use `npm test` and `npm run build` to verify
the frontend. Vite proxies `/api` and `/health` to `http://localhost:5080`.
`VITE_BRAIN_API_URL` may point the browser client to a different API origin.
Production hosting should use the same origin for UI and API or a deliberately
configured, trusted CORS policy.

## Appearance

The sun/moon button on the sign-in screen and in the shell header switches
between light and dark mode, including dashboard panels, status badges, forms,
and later-milestone placeholders. The control remains available on mobile and
supports keyboard focus and activation.

Without a saved choice, the UI follows `prefers-color-scheme`, including changes
while the page is open. An explicit choice overrides the system and persists as
only `brain-theme=light` or `brain-theme=dark` in local storage, across reloads
and sign-out. Credentials are never stored with this preference. Removing that
entry returns to the system default on the next reload. If the browser blocks
storage, switching still works for the current page and a console warning
explains that the preference cannot be saved.

A small same-origin external script applies the initial theme before styles
load, avoiding a light-theme flash without loosening the Nginx content security
policy. Native form controls and browser theme color follow the selected mode;
the existing reduced-motion behavior is preserved.

## PAV OS demo dashboard

**PAV OS** is an additional authenticated-shell page, not a replacement for
the operations dashboard or the authorized Sigma knowledge graph. It is an
explicitly fictional, read-only example of a personal operating system named
Studio North. It makes no graph API calls, does not change the configured
canonical vault, and adds no multi-vault backend or write capability.

The separate `ui/demo-vault/` contains 30 real Markdown documents:
`AGENTS.md`, three documents each in `business`, `personal`, `youtube`,
`content` and `career`, four skills, four routines, three memory/context notes,
and three application descriptions. These are all demo documents, not registered
executable skills, live application connections, or database memories. The
entry-point file explains where the other files live.

Vite imports only this fixture directory as raw text into the lazy-loaded
feature. Every dot corresponds to exactly one file; every directed relationship
comes from a relative Markdown `.md` link in its body. Reciprocal links remain
separate directed references in counts; the inspector distinguishes outgoing,
incoming and two-way connections. There are no invented semantic or folder
membership edges. Missing entry points, unsupported folders, invalid dates or
broken/escaping local links produce explicit errors rather than phantom nodes.
Document bodies render as plain text, never as HTML or executable Markdown.
**This bundled fixture is public static build content**, even though the page
is behind sign-in: never put private vault content, credentials or secrets here.
Live permission-filtered data continues to use the existing graph APIs.

The six views use D3 layouts/forces/scales and a high-DPI HTML Canvas 2D renderer:

| View | Arrangement |
| --- | --- |
| Ring | Anchored `AGENTS.md` at the center of five named tracks: Skills, Knowledge, Memory, Routines and Applications; all files of a kind stay on their track. Knowledge files form colored interest sectors. Portrait ellipses preserve mobile label spacing. |
| Circle | Documents around a common perimeter, with the entry point at the center |
| Areas | D3 hierarchical packing groups files into labeled folder neighborhoods |
| Links | A deterministic, bounded-tick D3 force layout emphasizes actual links |
| Timeline | Folder lanes and a UTC date scale use each document's `created` frontmatter, not fabricated file activity; same-day files stack within their lane |
| 3D Orbit | Depth-sorted perspective projection onto Canvas 2D; nodes, connections and projected orbital guides slowly rotate together by default (roughly 74 seconds per revolution), with manual rotation and pause controls; not a WebGL renderer |

Search matches names/paths; area selectors and legend buttons filter dots and
retain only links with visible endpoints. `AGENTS.md` remains visible as a
context anchor, including explicit no-match states. Selecting a dot or using
the keyboard-accessible **Jump to file** selector opens its source path, date,
body and actual connected files. Following a connected file outside a filter
clears the filter to reveal it. The chart uses an orange entry-point hexagon,
skill diamonds, ordinary file/memory dots, double-ring routines and application
hexagons; folder/area colors and the legend complement these shapes.

Drag a node to rearrange it. In **Ring**, dragging changes only its angle:
skills stay on the Skills ring, routines on Routines, and so on. The central
`AGENTS.md` stays anchored. Other views allow free placement. Drag empty
space to pan; scroll/button zoom keeps hit-testing and dragging in graph
coordinates. Select a file using **Jump to file**, focus the canvas and use
arrow keys for keyboard placement (rotation in Ring). **Reset node positions**
restores the current view's layout and camera; **Fit vault** resets only the
camera. Positions are temporary, per-view page state: filters, resizing and
theme changes preserve them, while leaving/reloading the page clears them.
Dragging never edits a file or its creation date/relationships.

Links retain static arrowheads and, by default, animate small particles from
the source document toward the referenced document along the same curved path.
Reciprocal references follow their own directed paths. This is an illustrative
view of Markdown references, **not live calls, runs, tool execution, or an
activity stream**. Turning **Links** off hides both edges and their particles.
**Pause motion** freezes link flow and automatic orbit; the orbit's own pause
control stops only rotation. Animation uses a bounded Canvas frame loop at
about 30 redraws per second, not React state updates for every frame, and
stops when hidden, unmounted or unnecessary. Orbit rotation pauses during
node dragging to keep the grabbed file stable.

Panning, scroll/button zoom, fit, and link/label toggles need no additional data.
The inspector remains usable
if Canvas is unavailable. The page supports both themes and responsive layouts.
Reduced-motion preferences disable all automatic flow/rotation, including
changes while the page is open; static arrows, dragging, keyboard placement and
manual orbit rotation remain available. The normal enabled motion preference
resumes when reduced motion is turned off.

To extend this **demo**, add a Markdown file under one of its nine folders with
an H1 heading and a valid `created: YYYY-MM-DD` field inside YAML frontmatter.
Connect it with ordinary relative Markdown links such as
`[Weekly review](../routines/weekly-review.md)`. File creation or edits here are
repository fixture maintenance, not writes to a running canonical vault.
Rebuild the UI container with `docker compose up -d --build --no-deps ui` (or
use Vite). No second JSON graph or generated node list needs maintenance.
The intentionally small fixture parser supports this documented format; it is
not a general-purpose Markdown/YAML vault importer.

## Authentication and safety

The UI requires only a dedicated administrator token in a password input.
It checks the token with `GET /api/agents/me` before showing the shell and sends
`Authorization: Bearer <token>` on each request, without `X-Agent-Id`.
The server maps this separate credential to `Authentication:UiAdminAgentId`;
it never infers an identity from browser-supplied input. The existing enabled
identity is resolved from the database on every request, so grant revocation and
disablement apply immediately. This is shared-token administration, not human
SSO; activity and audits use that configured identity.

`Authentication:UiAdminTokenHash` holds only the SHA-256 digest. Configure both
UI settings or neither; the digest must be distinct from every agent credential.
Outside Compose this authentication path is disabled by default. Agent clients
still require their bound ID/token pair, and MCP always requires agent
credentials. Do not simply omit the agent header when using an existing agent
token: it is not a UI credential.

Compose defaults the mapped identity to `willem-de-jong`; `BRAIN_UI_ADMIN_ID`
may select another explicitly provisioned identity. `Initialize-UiAdmin.ps1`
creates a random 256-bit `BRAIN_UI_ADMIN_TOKEN` and its digest in ignored `.env`
without replacing existing credentials. The startup script invokes it for both
new and existing development installations.

The token is held only in React memory: it is
not written to local storage, session storage, a cookie, a URL, or a log. A page
reload requires signing in again. The API remains authoritative for every
permission check; inaccessible dashboard sections show a permission-scoped
unavailable state. Reloading the page or signing out clears the in-memory
credential. Production requires HTTPS and an appropriately protected `.env` or
external credential provisioning; the development ports remain loopback-only.

The dashboard renders only explicitly selected fields. It never dumps arbitrary
audit metadata or registry metadata and never renders credentials or secret
values. Canonical document bodies and skill instructions are not loaded by the
dashboard.

## Existing API coverage and gaps

Current dashboard reads use:

| UI data | API | Notes |
| --- | --- | --- |
| Authenticated identity | `GET /api/agents/me` | Dedicated UI admin token; identity and effective grants resolved server-side |
| Agents | `GET /api/agents` | `agents.read`; server returns authorized records |
| Skills and tools | `GET /api/skills`, `GET /api/tools` | `registry.read`; backend filters by all manifest and dependency grants |
| Pending proposals | `GET /api/knowledge/proposals?status=pending&limit=100` | `knowledge.read`; the list is limited and follows proposal visibility rules |
| Metadata issues | `GET /api/knowledge/metadata/issues?limit=1000` | `knowledge.read` and `knowledge.admin` |
| Recent activity | `GET /api/audit?limit=8`, `GET /api/traces?limit=8` | `audit.read` / `traces.read`; backend visibility rules remain in force |
| API readiness | `GET /health/ready` | Authenticated database readiness check |

The existing API does not expose complete counts for memories or indexed
documents, agent connections, or background-job status/skill-sync failures.
Those require backend contracts before the dashboard can report trustworthy
metrics. This milestone does not add a dashboard aggregation endpoint. The
authentication extension reuses the same application permission enforcement.

## End-to-end verification

With the full stack running, install Chromium once with
`npx playwright install chromium` from `ui/`, then run `npm run test:e2e`.
The browser test uses the real Nginx proxy, ASP.NET authentication, and PostgreSQL
data. It rejects an invalid token, signs in with the ignored `.env` admin token,
checks successful identity and dashboard calls in both themes, verifies that only
the theme preference reaches local storage and session storage remains empty,
and signs out. A separate browser case checks system-theme changes, keyboard
toggling, reload persistence, mobile layout, and content security policy errors.
The tests do not enable Playwright traces or log the token.
Use `BRAIN_UI_URL` to select a different development UI URL.
The graph browser check also selects a real accessible document and verifies
the configured WebObsidian target, keyboard activation, new-tab isolation and
absence of Brain authorization/referrer headers. Only the external navigation
is intercepted; Brain API/data remain real. It does not sign into WebObsidian
or verify that a separately deployed instance contains the document.
The identity browser case reads actual agent/role/permission data, verifies
direct versus inherited grants, reviews and cancels an access replacement,
opens a bounded agent-focused graph, and checks role matrices, both themes,
mobile layout and offline recovery. It does not save changes to existing agents
or create persistent test identities. Actual mutations and denial/revocation
cases run against isolated PostgreSQL integration databases.

`ApiContractTests` cover invalid/unconfigured credentials, identity binding, MCP
isolation, and OpenAPI security alternatives. `UiAdminApiTests` use actual
PostgreSQL to verify current grants and immediate revocation/disablement.

## Knowledge graph

Choose **Knowledge graph** in the navigation. The isolated, lazy-loaded
`features/graph` feature uses Graphology and Sigma.js for a directed graph with
typed nodes, arrow edges, selection highlighting, panning, zoom and fit controls.
The initial global overview contains at most 80 entities, interleaved by type.
It is not a complete inventory. Type filters and case-insensitive name/source-ID
search run on the server, rather than loading a full graph to filter in the
browser. This is deterministic substring search, not semantic search.

The graph fills the available page width (without the dashboard's width cap)
and uses a viewport-scaled canvas, with the entity inspector below it.
There is no always-visible entity list beneath the canvas. Select a graph node
or use the compact, keyboard-accessible **Inspect entity** selector in the
toolbar to fetch its detail independently. **Expand neighbors** adds a one-hop neighborhood to the loaded
view. **Focus entity** replaces the overview with that entity's neighborhood;
the depth selector supports 0 (entity only), 1 or 2 hops. Relationships are
traversed in either direction but retain their original direction/type.
**Global graph** exits focus/search. Entity filters also constrain traversal;
the selected focus root is retained even if its type is unchecked.

The inspector shows only stable ID, type, canonical source reference, and
allowlisted document path/index time, agent enabled state or tool provider.
Loaded incoming/outgoing relationships support selection of their other endpoint.
Document bodies, skill instructions, arbitrary frontmatter, agent metadata,
registry descriptions and credentials are never graph payloads.

Loading, empty, source-invalid/unavailable, connection-error, forbidden and
no-longer-visible detail states are explicit. A 401 clears the application
session. Changing filters aborts pending loads/expansion and clears the old view;
stale responses cannot replace the new view. The browser retains at most 200
entities and 400 relationships across expansions and shows a bounded-view
notice when a source or browser budget is reached. Narrow a search or start
another focused view to explore beyond that budget.

Both themes and responsive layouts are supported. The accessible entity selector
and inspector remain usable without WebGL2; a visible message explains that
the visualization is unavailable. Reduced-motion preference removes camera
inertia and reduces wheel/double-click transitions; explicit camera buttons
and layout updates do not animate. There is no automatic animated layout or
live-activity overlay.

### Graph contracts and authorization

| Route | Contract |
| --- | --- |
| `GET /api/graph?types=document,skill&limit=80` | Bounded global overview; optional `query` substring |
| `GET /api/graph/search?query=deploy&types=skill,tool&limit=40` | Same projection and authorization, with a required query |
| `GET /api/graph/entity?id=skill:deploy-service` | One authorized entity, no neighborhood/body |
| `GET /api/graph/neighbors?id=skill:deploy-service&depth=1&limit=80` | Bounded neighborhood, including the root; optional type filter |

All routes require authentication and at least one source grant. No new graph
grant or administrator bypass exists. Documents require `knowledge.read`;
skills/tools require `registry.read` plus all current manifest/dependency
grants, using the same authorization helper as registry discovery.
Agents, roles and permission entities require `agents.read`. Without that grant,
registry requirements do not reveal permission nodes or their edges. Inaccessible
or nonexistent detail/expansion roots both return 404. Current identity/grants
are re-resolved on every HTTP request.

The wire model is `{ entities, relationships, truncated, depth }`. An entity has
`id`, extensible `type`, `label`, `source: { kind, id }`, and an allowlisted
string-valued `metadata` object. Stable IDs are `document:<vault-relative-path>`,
`skill:<id>`, `tool:<id>`, `agent:<id>`, `role:<id>` and `permission:<id>`.
Document IDs survive rebuilding the database index but change when the canonical
path changes. Encode IDs as query parameters; document paths use `/`, not host
filesystem paths. Relationship IDs hash source/type/target, with `type`,
`source` and `target` fields. Both endpoints must be visible and included in the
returned view.

`limit` must be 1-200 and includes the root for neighborhood queries; `depth`
must be 0-2, and query text is at most 200 characters. Types are a nonempty
comma-separated subset of the six supported types. Invalid values return 400.
At most 400 relationships are returned. `truncated` indicates a source,
traversal or relationship budget was reached, not a total count. The database
filters documents and identities before taking bounded candidate batches;
registry loading retains its existing 500-definition source bound.

### Sources, relationships and synchronization

There is no separately maintained graph catalog or graph writer. Graph
application services derive views from indexed knowledge, live read-only
manifests and current agent/role/permission data. The `KnowledgeGraph` migration
adds only rebuildable document reference arrays, a reference-parser version and
a GIN index for reverse-reference lookup. Apply it to an existing deployment
before running the updated hosts; Compose development startup applies it
automatically. The next knowledge scan populates references even for otherwise
unchanged indexed files.

Deterministic edges are `requires_tool` (skill to tool),
`requires_permission` (skill/tool to permission), `has_role` (agent to role),
`grants_permission` (agent direct grant or role to permission), and `references`
(document to explicitly referenced entity). Role-derived agent access is
represented by the agent/role/permission path, not a fabricated direct grant.
Registry changes and permission changes take effect on the next graph request
without a registry scan. Knowledge graph projection checks canonical revisions,
omitting stale/deleted/invalid files until indexing catches up.

To add explicit relationships, use a standalone top-level frontmatter field:

```yaml
---
graph_references:
  - document:Infrastructure/Proxmox/nacht-02.md
  - skill:deploy-service
  - tool:coolify
---
```

The field is optional, contains at most 100 valid stable-ID strings, and is
deduplicated. Unknown/inaccessible target records are omitted rather than
materialized as placeholder nodes. Nested fields, body text and inferred model
relationships do not generate edges. Invalid or duplicate reference fields,
aliases/tags in the reference list, and invalid reference IDs produce an
explicit knowledge-scan failure and preserve the previous index. Graph queries
still reject that now-stale index. Unrelated invalid YAML without the standalone
reference field does not prevent ordinary knowledge indexing.
Adding/editing references follows the existing canonical knowledge rules:
human filesystem/WebObsidian edits or agent proposals with explicit review and
the Brain Writer, never a direct graph/UI write.

Graph unit and PostgreSQL integration tests cover authorization across every
route, stable source identities, explicit references, stale/deleted sources,
depth/result bounds and large sources. Frontend tests cover the rendering
model, edge/node budgets, filtering, search, independent detail loads, focus,
expansion, stale responses and explicit failures. Chromium graph checks use the
real Compose API/data, including actual WebGL rendering, keyboard selection,
both themes, mobile layout, reduced-motion operation and the no-WebGL inspector.

## Agents and permissions

**Agents** lists up to 1,000 enabled/disabled identities with name, ID, team,
status and roles. Search and status filters narrow the returned inventory, not
the entire database. A limit notice avoids implying a complete inventory;
**Look up agent ID** fetches a record independently even beyond the list.
Selection loads authorized details independently before exposing controls.
Details explicitly render description, team, enabled state, roles, direct
grants, effective permissions and update time, never arbitrary agent metadata.

The agent permission matrix distinguishes grants inherited through each role,
direct grants, and the effective union reported by the backend. A permission
can be both direct and inherited without being counted twice. An enabled
identity, exact permission IDs and each operation's visibility/ownership rules
still apply: role names confer no implicit authority, disabling prevents
authentication, and grants do not bypass private memory or source constraints.
The current identity summary is refreshed with `/api/agents/me` whenever the
directory refresh succeeds; a failed load explicitly leaves only the sign-in
snapshot. Reload after newly granting directory access to a session that lacked
`agents.read` at sign-in.

**Permissions** also includes all configured roles (including unassigned
roles), their descriptions/grants and a role-by-permission matrix. Tables use
text rather than color to identify grants, keyboard-focusable scroll regions,
and horizontally bounded overflow on mobile. Light/dark themes, native keyboard
controls, loading, empty, offline, forbidden and missing-record states are
supported. Pending detail requests are cancelled when selection changes;
stale responses cannot expose another agent's controls. A 401 signs out.
A 403 clears affected data/forms rather than retaining old management controls.

### Identity read contracts

All catalog/detail operations require `agents.read` in the shared
`IAgentDirectory` boundary, not just the browser. Authentication and current
database grants are resolved on each request. Authorization precedes lookup,
so a caller without the read grant cannot distinguish a missing identity.

| Route | Result |
| --- | --- |
| `GET /api/agents` | Existing capped list of `AgentView` records |
| `GET /api/agents/{id}/details` | Independent `AgentView`, including disabled identities; 404 when missing |
| `GET /api/roles` | `{ id, description, permissions: string[] }[]`, including roles with no members |
| `GET /api/permissions` | `{ id, description }[]` from the database catalog, not a hard-coded browser list |

`AgentView.permissions` remains the sorted, deduplicated effective union for
compatibility. The additive `directPermissions: string[]` field identifies only
directly assigned grants on list/detail/create/update responses. `roles` retains
role IDs; the role catalog supplies their grant provenance. Catalogs and details
are current request snapshots, not an atomic snapshot across multiple requests.
No database migration, new grant or MCP management tool is introduced.
The explicit `/details` suffix keeps every valid agent ID (including `me`)
distinct from the existing `/api/agents/me` current-identity route. Registration
returns this independently readable resource in its `Location` header.

### Supported management boundaries

Registration uses the existing `POST /api/agents` operation with ID, name,
optional description/team and selected known roles/direct grants. Access edits
use the existing `PUT /api/agents/{id}/access` with
`{ roles, permissions, enabled }`; its `permissions` are **direct grants**, not
the effective union. Only controls backed by these operations are exposed.
Both require `agents.admin` server-side; the inspection-based UI additionally
requires `agents.read`. Role/permission definitions and credentials remain
operator-managed with no invented write or token-issuance API.

The form first shows an explicit review, exact replacement roles/direct grants,
enabled state and a catalog-derived effective-access preview. A separate
confirmation saves; the server revalidates actor access and known grants and
returns the actual effective union. Each grant collection is limited to 100
entries. Enabled-state changes affect authentication on the next request.
The server prevents changing the authenticated actor's own access; the UI
explains this instead of exposing an unusable self-edit control.

Access updates replace **all** roles, direct grants and enabled state. The
existing operation has no revision token or optimistic-concurrency protection;
concurrent access edits are not merged. Coordinate operator changes, refresh
before editing, and inspect the explicit replacement review before confirming.
Do not interpret the displayed update time as a concurrency token. Unknown
outcomes (for example, a dropped save response) are explicit: refresh to inspect
the actual state before retrying. Duplicate registration returns 409; invalid
grants return 400; denied/missing operations never report success.
Successful creates/access updates retain the existing transactional audit.

Registration creates an identity, not a usable credential. An operator must
provision a distinct token/digest out of band as described in the README.
There are no credential inputs, generated secrets or credential storage in
this feature. **Focus agent graph** opens the existing graph in a bounded
one-hop neighborhood; independent detail/expansion routes retain existing
source grants, two-hop maximum and 200-node/400-edge limits. Graph visibility
is based on the current viewer, not the selected agent's grants.

## WebObsidian linking

Select an accessible document node (or use **Inspect entity**) to view its
canonical vault-relative path and index time. **Open in WebObsidian** opens that
document in a new tab. Other entity types have no document action.
The link is built only after the independent, authorized entity-detail request
succeeds. A denied/missing detail does not produce a link.

The deployment contract is `<WebObsidianBaseUrl>/note/<vault-relative-path>`,
confirmed from the operator's existing WebObsidian note URL. For example,
`📂Projects/Homelab Brain/ui-prompt.md` becomes
`/note/%F0%9F%93%82Projects/Homelab%20Brain/ui-prompt.md`.
Brain and WebObsidian must expose the same canonical vault and relative paths;
this milestone does not synchronize vaults, translate path prefixes, or deploy
WebObsidian. An optional deployment prefix in the base URL is preserved.

`WebObsidianBaseUrl` is an operator-controlled, **public build-time setting**:

| Hosting | Configuration |
| --- | --- |
| Compose | Set `BRAIN_WEB_OBSIDIAN_BASE_URL=https://obsidian.example` in the ignored root `.env`; Compose passes it as the UI image's `WebObsidianBaseUrl` build argument |
| Direct image build | Supply `--build-arg WebObsidianBaseUrl=https://obsidian.example` when building `docker/Ui.Dockerfile` |
| Native Vite | Set `VITE_WEB_OBSIDIAN_BASE_URL` in the process environment or an ignored `ui/.env.local` before starting Vite/building |

For example, from `ui/` in PowerShell:

```powershell
$env:VITE_WEB_OBSIDIAN_BASE_URL = 'https://obsidian.example'
npm run dev
```

After changing the Compose setting, run
`docker compose up -d --build --no-deps ui`; merely restarting the container
does not replace a URL compiled into static assets. The setting defaults to
absent. Missing/invalid configuration shows an explicit inspector message and
omits the link; the rest of the graph remains usable.

Only absolute HTTP(S) base URLs without credentials, queries or fragments are
accepted. Use HTTPS outside local development and never put tokens or other
secrets in this setting. Targets use the canonical `source.id` returned by the
graph API, not document metadata or an arbitrary URL field. Path segments are
encoded separately, preserving folder separators and literal special
characters. Absolute paths, traversal, empty segments, backslashes, control
characters and malformed Unicode do not produce a link. Document text cannot
select another origin or add query/fragment parameters.

Links use `noopener`, `noreferrer` and a no-referrer policy. No Brain token,
identity, document body or shared login is sent to WebObsidian; it enforces its
own authentication/access. Brain authorization does not grant access there.
The Brain UI still cannot edit canonical Markdown. Pause the Brain Writer for
direct human editing/maintenance as described in
[safe knowledge writes](knowledge-writes.md), then resume with a clean checkout.
