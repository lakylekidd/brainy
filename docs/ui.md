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

The current shell opens PAV OS by default. Its compact Navigate menu labels
that page **Dashboard** and places Workspace and Governance in a left stack,
with Control plane and System in a right stack. It also contains Agents,
Permissions and the later-milestone placeholders. The header retains
the BRAINY brand, shared-knowledge tagline, signed-in username, theme toggle,
and sign-out control. Dashboard and Knowledge Graph views are not mounted in
the current shell. The underlying dashboard and graph feature code and API
contracts remain in the repository for possible future use; canonical document
editing remains in WebObsidian.

The PAV OS dashboard keeps its full-width layout. Other shell pages use a
centered content column capped at 1360 pixels with 50 pixels of space below the
header, plus a footer showing the current year, Brainy contributors, and the
PolyForm Noncommercial License 1.0.0.

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

## UI development guidelines

Brainy is developed mobile-first. Every UI change must be designed and checked
at narrow mobile widths before it is considered complete, including navigation,
dialogs, forms, tables, labels, spacing, and touch targets. Preserve the
desktop layout while ensuring content remains readable, usable, and free of
horizontal overflow on small screens. Add or update responsive tests when a
change affects a shared shell or reusable component.

## Appearance

The entire UI adapts the supplied AI OS Dashboard Current 1.1.1 reference:
its night/live/orange-core palette, compact header with a popover navigation
menu, hairline separators and small control radii.
Dark surfaces use `hsl(205 22% 6%)` / `hsl(205 25% 9%)`, the accent is
`#f07a2e`, secondary text `#b0bec8` and muted text `#81919b`.
JetBrains Mono and Barlow Condensed are bundled under `ui/public/fonts`;
their SIL Open Font Licenses and original source/hash provenance are adjacent.
There are no remote font requests or new font dependencies.

Navigation opens from the Navigate button on desktop and mobile. Opening moves
focus to the active route; Escape or route selection closes the menu and returns
focus to its trigger. The compact top row remains visible at narrow widths.
Navigation uses URL paths such as `/dashboard`, `/agents`, `/skills`, and
`/tools`; refreshing the browser preserves the current page.
The reference's calendar, mail, execution and other integrations are not added.

The sun/moon button on the sign-in screen and in the shell header switches
between light and dark mode, including dashboard panels, status badges, forms,
and later-milestone placeholders. The control remains available on mobile and
supports keyboard focus and activation.

Without a saved choice, the UI defaults to the reference dark theme, regardless
of `prefers-color-scheme` or subsequent system changes. An explicit choice persists as
only `brain-theme=light` or `brain-theme=dark` in local storage, across reloads
and sign-out. The administrator token is stored separately in tab-scoped
`sessionStorage` so a reload in the same tab can restore the sign-in. The app
revalidates it with `GET /api/agents/me` before showing authenticated content;
sign-out or a rejected token clears it. Closing the tab normally ends that
session. Since same-origin JavaScript can read session storage, treat the
token as a secret and keep the UI protected from untrusted scripts. Removing
the theme entry returns to dark mode on the next reload. If the browser blocks
storage, switching still works for the current page and a console warning
explains that the preference cannot be saved.

A small same-origin external script applies the initial theme before styles
load, avoiding a light-theme flash without loosening the Nginx content security
policy. Native form controls and browser theme color follow the selected mode;
the existing reduced-motion behavior is preserved.

## PAV OS demo dashboard

**PAV OS** is the default authenticated-shell page. It is an
explicitly fictional, read-only example of a personal operating system named
Studio North. It makes no graph API calls, does not change the configured
canonical vault, and adds no multi-vault backend or write capability.

The separate `ui/demo-vault/` contains **134 real Markdown documents**:
one `AGENTS.md`, six area overviews, 110 notes, five skills, six routines,
three memory/context notes and three application descriptions. Business has
25 files, Personal 24, YouTube 24, Content 24, Career 23 and System 13,
excluding the root. These are all demo documents, not registered
executable skills, live application connections, or database memories. The
entry-point file explains where the other files live.

Vite imports only this fixture directory as raw text into the lazy-loaded
feature. Every graph node, including each large overview hub, corresponds to
exactly one file. **598 directed references** come from relative Markdown `.md`
links in document bodies. Reciprocal links remain
separate directed references in counts; the inspector distinguishes outgoing,
incoming and two-way connections. Separately, **133 dashed membership guides**
connect the root to real overviews and each overview to its same-area files.
They are inferred from area metadata, not Markdown references, and do not
inflate reference counts or animate as calls. **Membership** and **Links** have
independent toggles; no semantic edges, execution records, folder nodes or
duplicate/phantom file dots are created.
Missing entry points, unsupported folders/metadata, duplicate area overviews,
invalid dates or broken/escaping local links produce explicit errors.
Document bodies render as plain text, never as HTML or executable Markdown.
**This bundled fixture is public static build content**, even though the page
is behind sign-in: never put private vault content, credentials or secrets here.
Live permission-filtered data continues to use the existing graph APIs.

The six views use D3 layouts/forces/scales and a high-DPI HTML Canvas 2D renderer:

| View | Arrangement |
| --- | --- |
| Ring | Anchored `AGENTS.md` at the center of five named tracks: Skills, Knowledge, Memory, Routines and Applications. Real overview hubs sit inside their notes; size-weighted area sectors pack knowledge into up to four rows. Other kinds stay on their own tracks. The layout uses a 28px vertical inset to enlarge the tracks while retaining space for outer nodes and labels. Portrait ellipses preserve mobile label spacing. |
| Circle | Area-grouped perimeter sectors, with curved connections bowed toward the center |
| Areas | A deterministic D3 force layout groups files into compact same-color clouds around real initialed overview hubs; applications stay near the nucleus. Entering this view automatically fits the layout inside the chart with 32px of breathing room. |
| Links | A deterministic D3 force layout emphasizes actual references while retaining weaker area/membership anchors. Entering this view automatically fits the full layout with 32px of breathing room. |
| Timeline | Kind/layer lanes and a UTC date scale use each document's `created` frontmatter, not fabricated file activity; same-day files stack within their lane |
| 3D Orbit | Depth-sorted Canvas 2D projection: skills in an inner shell, knowledge/memory in a middle shell, routines outside, applications on an equatorial ring; area sectors determine longitude. Guides and nodes turn together (roughly 57 seconds per revolution), with a gentle 27-second tilt sway and manual controls; it enters fitted to the chart with 48px of breathing room; not WebGL |

The page uses the same reference three-column composition: local clock, demo
inventory and the entry-point guide on the left; the graph in the center; and
routine cards plus example memory/context file links on the right. The Skills
deck is removed. These widgets read the bundled fixture, not live schedules,
database memories or external applications. Each routine card shows its
fixture-specific icon and title with a per-routine model/effort preference.
The settings matrix offers Haiku, Sonnet, Opus and Fable across Low, Medium,
High, XHigh and Max effort; the selected model row has a blue range spanning
Low through the active effort cell, with a white knob and orange center marker.
The range appears only in the settings panel, which has a close button. Choose
effort by clicking or dragging horizontally across the model row; keyboard radio
selection remains available.
Selections are saved in browser local storage and are not sent to a model.
Clicking a routine title opens its source in the chart inspector. Play starts a
demo-only preview timer without opening the inspector, disables that card's
play control while active, and shows elapsed time. Stopping the preview records
its elapsed display for that page session next to the card controls without
increasing card height. No routine or model executes, and the progress animation
does not represent task progress.
The centered PAV OS title, page note, and global shell status footer are omitted
to give the map more room below the persistent BRAINY shell header. Side rails
are kept narrow so the map gets more horizontal space, and it has no permanent
"In your orbit" inspector.
Its normal Canvas height fills more of the viewport (viewport height minus
275px, clamped to 560–960px on desktop; 58dvh, clamped to 440–620px on mobile).
**Fullscreen** uses the browser Fullscreen API to
fill the viewport, hiding the surrounding shell while keeping graph controls.
Exit using its button or the browser's Escape action. Resizing/fullscreen keeps
temporary node placements. Unsupported or rejected fullscreen requests show an
explicit message without disabling the normal map.

Search matches names/paths; area selectors/legend buttons and the independent
file-type selector filter dots and retain only relationships with visible
endpoints. `AGENTS.md` remains visible as a context anchor, including explicit
no-match states. Matching files also retain their real area overview even when
that overview does not match the search or type. Legend/inventory counts show
the whole fixture; hub labels count visible area files, including the overview
once. Selecting an overview highlights its area, frames its files with the
camera and exposes an accessible **Area members** list in the inspector.
Selecting a dot or using
the keyboard-accessible **Jump to file** selector opens its source path, date,
body and actual connected files in a dismissible overlay, initially closed.
Dragging selects without opening the overlay; **File details** can reopen it.
Click the selected dot again, tap empty graph space or press **Escape** in the
map to clear file/ring focus and close the overlay. The camera smoothly returns
to the view's default framing without changing temporary placements. Initial
and cleared graph selection is empty; `AGENTS.md` remains the entry-point
context, not an implicitly selected dot. Clicking it still selects and opens
its document, and clicking it again clears it like any other node. The Jump
selector and details control fall back to `AGENTS.md` when nothing is selected.
Explicit navigation through Jump, widgets and document links always selects
the destination rather than toggling it. The overlay's close button clears the
selection, closes details, and returns focus to the details control while the
camera returns to the view's default framing.
The overlay's expand/restore control lets the selected document fill the chart
panel for reading its full details, with its own scrolling area; selecting a
different node returns the inspector to its compact position.
Escape also returns that focus when invoked from the open inspector.
Following a connected file outside a filter
clears the filter to reveal it. The chart uses an orange entry-point particle network,
large initialed overview circles, skill diamonds, ordinary file/memory dots,
double-ring routines and application hexagons. Area colors apply to all kinds:
a Content skill stays purple on the Skills track, rather than acquiring a
generic Skills color.

In **Ring**, click a track's name (**Skills**, **Knowledge**, **Memory**,
**Routines**, **Applications**) to focus every visible file on that layer,
across all areas. Matching dots and their incident reference links brighten;
unrelated nodes dim rather than disappear, and `AGENTS.md` stays visible as
context. This does not alter area/type filters, graph counts, positions or files.
Click the same name again or press **Escape** while focus is in the map to clear.
Ring names are also keyboard buttons: **Tab** to one and press **Enter/Space**.
Their bounds follow the painted track through pan, zoom, breathing, resize and
fullscreen; a direct hit on a real dot takes priority over overlapping text,
preserving node dragging/inspection. Cancelled presses or label drags do not
toggle a ring. Focusing a ring closes the single-file inspector; selecting a
file, changing layouts or changing search/area/type filters clears ring focus.
The ordinary **Labels** toggle controls file names, not these ring controls.

Drag a node to rearrange it. In **Ring**, dragging changes only its angle:
skills stay on the Skills ring, routines on Routines, and notes stay on their
assigned knowledge row. The central
`AGENTS.md` stays anchored. In **Areas**, its overview hub gently pushes nearby
member nodes aside; if dragged farther away, the cluster follows it. Other
views allow free placement. Drag empty
space to pan; pans, node drags, cancelled presses and multi-pointer background
gestures do not clear selection. Scroll/button zoom keeps hit-testing and dragging in graph
coordinates. Select a file using **Jump to file**, focus the canvas and use
arrow keys for keyboard placement (rotation in Ring). **Reset node positions**
restores the current view's layout and camera; **Fit vault** resets only the
camera. Positions are temporary, per-view page state: filters, resizing and
theme changes preserve them, while leaving/reloading the page clears them.
Dragging never edits a file or its creation date/relationships.
Selecting an area overview in Areas or Links smoothly frames its member files,
and deselecting smoothly restores that view's default camera. Both transitions
are immediate when reduced motion is enabled.

Links retain static arrowheads and animate bounded glowing signal trails from
the source document toward the referenced document, followed by a soft arrival
halo. Particles, arrows and edges share the same path. Arc-length sampling
keeps travel even on bowed Circle connections; traversal is bounded to 1.6-6
seconds based on distance/zoom. Ambient traffic is limited to eight links,
or four on narrow canvases; focusing a file limits signals to its references.
Incoming references still flow toward the selected file, never backward simply
because it is selected. This is an illustrative
view of Markdown references, **not live calls, runs, tool execution, or an
activity stream**. Turning **Links** off hides both edges and their particles.
**Pause motion** freezes automatic effects; the orbit's own pause control
stops only orbital rotation/sway, leaving the decorative nucleus alive.
Orbit speed eases to a stop while hovering or selecting a non-root file,
and eases back in after returning to `AGENTS.md` and leaving the dots.
Animation uses a bounded Canvas frame loop at
about 30 redraws per second, not React state updates for every frame, and
stops when hidden or unmounted; a settled Timeline with links off needs no
animation loop. Orbit rotation and the radial ripple pause during dragging
to keep the grabbed file stable.

The supplied AI OS Dashboard Current 1.1.1 was inspected as the behavioral,
layout and theme reference. The implementation remains React/TypeScript using
the existing D3 dependency, file graph and five capability tracks; it does not
embed or start that project's server, sample data or integrations. Appearance
is adapted into shared React styles; only the licensed font assets are bundled.
Its graph-specific settings are adapted here:

- 360 decorative core particles and nearby connecting segments rotate and
  breathe around the one actual `AGENTS.md` node; they are not extra files,
  relationships or activity data.
- Coherent radial motion stays within 1.1% of the base radius. Ring nodes and
  their ellipse share the same wave; there is no random displacement that
  breaks track membership. Colored neighborhood glows breathe softly over a
  subtle static hexagonal background.
- Layout changes use 820ms cubic easing with up to 160ms area staggering.
  Reduced motion or paused motion applies the target directly. Node emphasis
  eases toward 1.4x size; unrelated files dim rather than disappear.
- Areas uses 34px / 0.05-strength links, -6 charge (-34 for applications),
  100px maximum charge distance and stronger area/application anchors.
  Links uses 40px / 0.22-strength links, -16 charge and 240px maximum charge
  distance. Both use collisions, pin the root and stop D3 before bounded
  manual settling (220/260 ticks); no additional simulation timers escape.
  Membership forces bring same-area files toward their overview independently
  of actual reference links; size-weighted area anchors keep clouds separate.
- **Dot size** defaults to 85%, ranges from 50-125%, and scales down further
  on narrow canvases. Like placements, it is page state, not a file edit.
  Labels use overlap checks, zoom hysteresis and Orbit depth hysteresis;
  overview and selected/hovered names have priority and move at subpixel coordinates.

Panning, scroll/button zoom, fit, and link/label toggles need no additional data.
The inspector remains usable
if Canvas is unavailable. The page supports both themes and responsive layouts.
Reduced-motion preferences disable all automatic motion, including
changes while the page is open; static arrows, dragging, keyboard placement and
manual orbit rotation remain available. The normal enabled motion preference
resumes when reduced motion is turned off.

To extend this **demo**, add a Markdown file under an area folder (`business`,
`personal`, `youtube`, `content`, `career`, `system`) or a capability folder
(`skills`, `memory`, `routines`, `applications`), with an H1 heading and a valid
`created: YYYY-MM-DD` field inside YAML frontmatter. Nested folders are supported;
depth organizes source files but does not create extra graph nodes.
The owning area defaults to the top-level area folder, or System for capability
folders. Optional scalar `area:` metadata can assign a capability to an interest
area. The kind defaults to `note` in area folders or the singular capability
kind; optional `kind:` accepts `area`, `note`, `skill`, `memory`, `routine` or
`application`. Layer/track derives from kind, independently of owning area.
For example, a skill in the Content area can live in `skills/`:

```markdown
---
created: 2026-10-18
area: content
kind: skill
---
# Example writing skill

[Content overview](../content/index.md)
```

Each area can have at most one real `kind: area` overview. Current hubs are
`business/index.md`, `personal/personal.md`, `youtube/index.md`,
`content/index.md`, `career/index.md` and `system/index.md`.
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
and signs out. A separate browser case checks dark defaults under either system
theme, stability through system-theme changes, keyboard
toggling, reload persistence, mobile layout, and content security policy errors.
The tests do not enable Playwright traces or log the token.
The reference-layout case verifies the exact dark tokens, served font hashes
and loaded fonts, three-column proportions, cross-page navigation, mobile
keyboard/focus behavior and overflow. Desktop Dashboard, PAV, identity and
mobile screenshots support visual comparison against the supplied reference.
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

The standalone graph fills the available page width and uses a viewport-scaled
canvas, with the entity inspector below it. Its Dashboard embed fills the
center column and hides only the empty inspector until an entity is selected.
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
the entire database. A limit notice avoids implying a complete inventory.
Selecting an agent or clicking **Inspect** opens its authorized details in a
modal. Details explicitly render description, team, enabled state, roles,
granted permissions and update time, never arbitrary agent
metadata.

The agent details modal shows role-inherited permissions as compact labels, with
a live status dot and the role source after a colon. Direct grants appear in
their own section below the granted permissions, using the same label format. A permission can be both direct and
inherited without being counted twice. An enabled
identity, exact permission IDs and each operation's visibility/ownership rules
still apply: role names confer no implicit authority, disabling prevents
authentication, and grants do not bypass private memory or source constraints.
The current identity summary is refreshed with `/api/agents/me` whenever the
directory refresh succeeds; a failed load explicitly leaves only the sign-in
snapshot. Reload after newly granting directory access to a session that lacked
`agents.read` at sign-in.

**Permissions** also includes all configured roles (including unassigned
roles), their descriptions/grants and a role inspector. The role selector and
role description appear above a scrollable permission table with a permission
ID and a user-facing description for each grant. Those descriptions come from
the permission definitions and are refreshed for existing catalog rows during
application initialization in every environment.
Light/dark themes, native keyboard controls, loading, empty, offline, forbidden
and missing-record states are supported. Pending detail requests are cancelled when selection changes;
stale responses cannot expose another agent's controls. A 401 signs out.
A 403 clears affected data/forms rather than retaining old management controls.
The Permissions view contains only the role and permission catalog; the agent
directory, search, details and registration controls remain in the Agents view.

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
| `POST /api/roles` | `agents.admin`, `{ id, description, permissions: string[] }`; creates a role from known permission IDs and records an audit event |

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
requires `agents.read`. `POST /api/roles` creates a lowercase role ID,
description and up to 100 known permissions; it does not assign the role to any
agents. The Permissions page exposes creation only with `agents.admin` and
reviews the ID, description and selected permissions before confirmation.
Existing role editing, permission-catalog changes and credential issuance
remain operator-managed.

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
