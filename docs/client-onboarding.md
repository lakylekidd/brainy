# Brain Client and device onboarding

Module 19 introduces a .NET 10 `brain` CLI and device-scoped access to the
existing Brain API. The API remains the authority for identity, grants, registry
visibility and provisioning approval. This guide describes the implemented
slice and its remaining limits; Module 19 is **not yet complete**.

## Identity and device credentials

Configure an external OIDC provider that issues JWT access tokens for the Brain
API. Brain validates issuer, audience, signature and expiry through ASP.NET Core
JWT bearer authentication. Set:

```text
Authentication:Oidc:Authority=https://identity.example.com/
Authentication:Oidc:Audience=brain-api
Authentication:Oidc:ClientId=brain-cli
Authentication:HumanAgentBindings:<exact-subject>=<existing-agent-id>
```

The public client is registered at the provider for Authorization Code with
PKCE and, when headless login is needed, device authorization. It needs the
`openid profile brain.device` scopes, no client secret, and a loopback redirect
for browser login. Brain checks the exact `sub` binding and `brain.device` scope;
the mapped, enabled Brain identity must have `devices.register`. Provisioning
plan/approval additionally requires `runtime.provision`, `runtime.read` and
`registry.read`. The mapping is operator-managed, not a caller-supplied agent ID.
There is no built-in identity provider or human password database.

`GET /.well-known/brain-client` exposes only issuer, public client ID and scopes
so `brain login --server` needs no local provider configuration. It returns 503
until OIDC is configured. Remote client/API and OIDC connections require HTTPS;
the client permits HTTP only for loopback development.

On registration, Brain returns a random `bd_` credential once, stores only its
SHA-256 digest, and binds it to a device UUID, the human subject and a Brain
agent ID. Device permissions are an explicit subset of that agent's current
grants. They never include agent administration, device revocation of others,
or provisioning. Every request rechecks the enabled agent and intersects its
current grants with stored device scopes. No agent ID in a request body grants
access. A device credential can access `/mcp` without `X-Agent-Id`; the server
resolves the bound identity. Existing agent credentials still require the
bound header/token pair. UI administrator credentials remain separate.

Enrollment detects installed supported harnesses and records their names as
device metadata. Heartbeats report both installed and connected harness names;
the server audits connection changes. The CLI keeps only the server URL, device UUID, connected
harness names and device name in `~/.brain/client.json`. The credential lives in Windows
CurrentUser DPAPI encrypted storage, Linux Secret Service through `secret-tool`,
or macOS Keychain. Login fails if the OS store is unavailable. The Runtime Agent
must run as the same user that owns this credential. Rotation invalidates the
old credential immediately; revocation removes the digest and denies the next
request. `brain logout` requires a successful remote revocation before removing
the local credential.

## CLI and installation

Publish a single-file, self-contained executable for a supported runtime ID:

```powershell
dotnet publish src/Brain.Client -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The build also publishes for `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`
and `osx-arm64`. Distribute the published `brain`/`brain.exe` through a trusted
channel. Separate `scripts/Install-BrainClient.ps1` and
`scripts/install-brain-client.sh` copy a local single-file package after an
operator-supplied SHA-256 check. They do not download binaries or credentials.

```text
brain login --server https://brain.example.com
brain login --server https://brain.example.com --headless
brain status
brain devices
brain connect claude-code|codex|hermes
brain disconnect claude-code|codex|hermes
brain skills list
brain skills sync
brain runtimes inspect
brain runtimes report
brain dependencies check
brain doctor
brain rotate
brain logout
```

`brain install` installs the Runtime Agent as a systemd **user** service on
Linux or launchd LaunchAgent on macOS; `brain uninstall` removes only its own
service file. `brain agent run` runs the same loop in the foreground. Windows
Service installation is not implemented: the service account cannot safely
reuse an interactive user's DPAPI credential. Run the agent manually under
the enrolled user until a dedicated service identity and credential transfer
are implemented. This is a Module 19 acceptance gap.

## Harness connections and skills

`IHarnessAdapter` has Claude Code, Codex and Hermes implementations. Each uses
its harness's MCP CLI to add a single named `brain` stdio entry, preserving other
servers/settings. It refuses an existing Brain entry, verifies the resulting
listing, and can remove only an entry this client recorded as connected. The
entry runs the installed `brain mcp-proxy` executable. That proxy reads the
device credential from the OS store and forwards MCP JSON-RPC over authenticated
Streamable HTTP; harness configuration contains no token. Actual harness CLI
compatibility remains to be exercised on each supported OS/version.

`brain skills sync` reads only permission-filtered skills from the existing
canonical registry API. Each skill response includes a SHA-256 hash of its
instructions, which the client checks before writing. It records the registry
source hash and local instructions hash in `.brain-sync.json`, and refuses to
overwrite an unknown or locally modified `SKILL.md`. Copies are written to
`~/.brain/skills`, plus `brain-<id>` directories under the connected harness's
user skill directory. Only documentation is copied; no skill script runs.
An offline sync leaves existing copies untouched. The client reports per-skill,
per-harness `synced`, `conflict` or `stale` status to Brain. Registry index
changes emit a narrow authenticated SSE hint; `brain agent run` also reconciles
every five minutes. The event stream closes after one minute so reconnects
recheck device revocation and permissions. Neither notification nor the
database is a second canonical skill source.

## Runtime reports and dependencies

The Runtime Agent sends a typed host report with OS, architecture, installed
allowlisted CLI tools and their version output, Docker daemon availability and
health. Reports identify an execution environment as `host`, `container`,
`remote` or `harness`. The included agent reports the host only; other
environment inspectors are not yet implemented. A host executable is never
inferred to exist in a container, remote host or harness environment.

Skill manifests may add an optional list such as:

```yaml
runtime_requirements: [host:python3>=3.10, container:graphify]
```

The grammar is `<environment-kind>:<command>[>=numeric.version]`.
`GET /api/devices/{id}/dependencies` joins authorized live skills with that
device's reported environments and returns availability or a reason. Unknown
or unhealthy environments do not satisfy requirements. This is discovery,
not permission to install a package.

## Provisioning

Provisioning is disabled until an operator configures an exact package policy.
An entry under `Provisioning:Packages` specifies `Requirement`, `PackageId`,
`Version`, `WheelUrl`, `Sha256`, `OperatingSystem` and `Architecture`. The URL
must be HTTPS and point to a `.whl` file. A human with a valid OIDC token and
`runtime.provision` grant may run `brain provision <requirement>` only when that
requirement is missing from an authorized skill. A separate `brain approve
<plan-id>` action changes the pending plan to approved. Both actions are
audited. The Runtime Agent polls only approved plans, atomically claims one,
downloads at most 50 MiB without redirects, verifies the artifact SHA-256 and
wheel identity, and invokes the allowlisted `pipx` installer with no shell and
`--no-deps`. It then verifies the required host command and reports completion
or failure. No command from a skill is executed. Plans for container, remote or
harness environments are not supported by this installer.

Only pinned, independently reviewed wheel artifacts should be placed in the
policy. An absent matching policy fails closed. Provisioning requires `pipx`
under the enrolled user and does not grant administrator privileges. There is
currently no automatic retry of a failed plan; a running plan older than 30
minutes can be reclaimed after a Runtime Agent interruption.

## API and audit

Device routes include register/list/get/rename/heartbeat/rotate/revoke,
runtime report/list, dependency checks, skill sync report/list, provisioning
plan/approve/list/queue/start/complete and the skill-change event stream.
Registration, planning and approval require OIDC. Device reporting/queue
operations require the bound device credential; ordinary reads honor current
permissions and ownership. A database migration creates `devices`,
`device_runtimes`, `device_skills` and `provisioning_plans`.

Audits cover device registration, renaming, heartbeats, harness connection
changes, rotation/revocation, runtime reports, sync outcomes and provisioning
transitions. Neither tokens nor installer output are stored in audits. See
[activity tracing](activity-tracing.md)
for the separate bounded trace stream.

## Remaining Module 19 work

- Windows Service installation and a safe service-identity credential handoff.
- Real OIDC-provider and installed-harness end-to-end checks on Windows, Linux
  and macOS. Cross-RID publish and in-memory OIDC tests do not replace them.
- Inspectors for container, remote and harness-specific runtime environments.
- Automatic client/Runtime Agent upgrade workflow and unattended recovery when
  credential rotation succeeds on the server but fails in the OS store.
- Broader approved installers/execution images beyond pinned user-level wheels.
- Re-enrollment of an existing device and dedicated harness connection activity
  events. Connection transitions are currently audited and reflected in device
  metadata, but no separate live event is emitted.

Keep the milestone incomplete until these gaps and the pasted acceptance
criteria are verified.
