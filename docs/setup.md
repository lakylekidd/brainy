# Local setup

This guide covers the local development stack. For architecture and service boundaries, see [architecture](architecture.md). For API credentials and endpoints, see [api](api.md).

## Requirements

The complete stack requires PowerShell 7, Git, and Docker with Linux containers. The .NET 10 SDK and Node.js 24 are needed for native builds and tests outside the containers.

## Start the stack

```powershell
.\scripts\Start-Development.ps1
```

The script starts PostgreSQL/pgvector, the API, the worker, and the built UI. Open `http://localhost:5173` and enter `BRAIN_UI_ADMIN_TOKEN` from the ignored `.env` file. No username or agent ID is required. This is a shared administrator credential, not a personal SSO account.

The startup flow preserves existing credentials, database volumes, and the dedicated vault. It creates `.env` only when absent and adds a distinct UI token/digest only when both UI credential settings are missing. Partial or invalid credential pairs fail rather than being overwritten. Only digests reach the API container. Never commit `.env`.

If PowerShell 7 is unavailable but the .NET SDK is installed, use a local tool installation:

```powershell
dotnet tool install PowerShell --tool-path .\.local\tools
.\.local\tools\pwsh.exe -NoProfile -File .\scripts\Start-Development.ps1
```

## Services and ports

| Service | Local address |
| --- | --- |
| UI | `http://localhost:5173` |
| API | `http://localhost:5080` |
| PostgreSQL/pgvector | `localhost:55432` |

Published ports bind to loopback. The UI's Nginx server proxies API requests on the same origin. PostgreSQL data persists in the `brain-postgres` volume.

```powershell
docker compose ps
docker compose logs --tail 100
docker compose down
```

`docker compose down` preserves the database volume. Run the startup script again to rebuild after changes.

## Frontend hot reload

To use Vite instead of the built UI container:

```powershell
docker compose stop ui
Set-Location .\ui
npm ci
npm run dev
```

Open the Vite URL shown in the terminal and sign in with the same UI token. Vite proxies API requests to `http://localhost:5080`; `VITE_BRAIN_API_URL` can select another trusted API origin. Production deployments require HTTPS and should use a same-origin reverse proxy.

## Vault and development data

The vault initializer copies `sample-vault` into ignored `.local/vault`, creates a dedicated Git repository and initial commit, and preserves existing initialized vaults. It never commits vault changes to this application repository. `BRAIN_VAULT_PATH` may override the Compose mount source. On Linux, set `BRAIN_WRITER_UID` and `BRAIN_WRITER_GID` to the writable vault owner's IDs.

Compose applies EF migrations and seeds the development identities `willem-de-jong` and `codex` in the `platform` team. The worker removes expired memories and scans the vault and registry every 30 seconds, processes approved proposals every 5 seconds, and runs deterministic metadata scanning every 300 seconds. Write processing is enabled explicitly in Compose and disabled by default elsewhere.

The seeded `metadata-enrichment-worker` identity has knowledge read/propose grants but no credential. It queues frontmatter proposals for explicit review; semantic model calls are disabled. See [metadata enrichment](metadata-enrichment.md).

To exercise knowledge writes in development:

```powershell
.\scripts\Test-Development.ps1 -TestKnowledgeWrites
```

This leaves a test Markdown document, proposal/audit records, and a Git commit in the development vault. The default smoke test only reads knowledge and tests disposable memory operations.

## Production boundary

Compose is for local development. Production needs operator-provisioned identities and roles, external credential configuration, TLS, restricted database access, backups, and reviewed migrations. Automatic development seeding is rejected outside Development. No external secret retrieval is implemented.
