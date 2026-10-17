# Brain UI

The Brain UI is an operations and exploration surface, not a Markdown editor.
WebObsidian remains the place to create and edit canonical knowledge documents;
the Brain UI must link out to it rather than copy editing functionality.

## Current scope: UI Milestone 1

The frontend is a React + TypeScript + Vite application under `ui/`. Its current
structure is:

```text
ui/src/
  api/client.ts
  components/
  features/dashboard/
  App.tsx
  styles.css
```

The shell exposes the planned navigation, while later milestone routes are
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

`ApiContractTests` cover invalid/unconfigured credentials, identity binding, MCP
isolation, and OpenAPI security alternatives. `UiAdminApiTests` use actual
PostgreSQL to verify current grants and immediate revocation/disablement.

## Proposed graph model and loading strategy

There is no generic graph entity model or graph API in the current backend.
When that milestone is approved, use stable `entities` and `relationships`
records with IDs, extensible type/relationship strings, JSON metadata and
references to source records. Existing source-of-truth data should remain
canonical: document entities reference indexed knowledge documents, skill/tool
entities derive from the operator-managed manifests, and agent/role entities
derive from the Brain database. Do not duplicate Markdown or manifest bodies in
a graph store. Deterministic relationships (manifest tool dependencies,
permissions, and explicit metadata references) should be generated from their
source data.

The anticipated API surface is `GET /api/graph`, entity detail and neighbors
routes, and graph-specific search/semantic routes. Return bounded subgraphs:
filter and search server-side, cap depth and result size, then load neighbors
on demand. A global graph should never require sending every entity to the
browser. Any eventual graph query must apply the caller's authorization before
returning nodes, edges, or metadata.

## WebObsidian linking

No link is currently generated because the WebObsidian deployment URL and its
document route are not configured in this repository. The graph milestone
should accept an operator-configured `WebObsidianBaseUrl`, build links from the
canonical relative vault path using the documented WebObsidian route, and
encode path segments safely. Do not hard-code a hostname or allow document
metadata to supply an arbitrary link origin.
