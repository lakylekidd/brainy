# Development and verification

## Build and test

Restore tools and run the server tests from the repository root:

```powershell
dotnet tool restore
dotnet build HomelabBrain.sln
dotnet test HomelabBrain.sln
```

The UI has its own package lock and checks:

```powershell
Set-Location .\ui
npm ci
npm test
npm run build
```

With the Compose stack running, run the browser suite:

```powershell
npx playwright install chromium
npm run test:e2e
```

The browser checks reject invalid login, verify dashboard requests against the backend, ensure credentials are not stored, and exercise graph rendering, search, filters, expansion, focus, depth, keyboard inspection, and the no-WebGL accessibility case. `BRAIN_UI_URL` overrides the local UI URL. Traces are disabled to avoid recording credentials. The broader `scripts/Test-Development.ps1` smoke test covers memory/knowledge round trips, pgvector, registry discovery, and MCP.

Unit tests run without a database or network. Git writer tests require the Git CLI and temporary repositories. Integration tests use isolated PostgreSQL 17 + pgvector containers by default, apply real migrations, and clean up only their test databases. Set `BRAIN_TEST_POSTGRES` to use an operator-supplied PostgreSQL connection instead.

## Host-based development

Start PostgreSQL with `docker compose up -d postgres`, then configure `ConnectionStrings__Brain`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__MigrateOnStartup=true`, `Database__SeedDevelopment=true`, `Knowledge__VaultPath`, and the agent token hash variables from `.env`. Run the API with:

```powershell
dotnet run --project src/Brain.Api --urls http://localhost:5080
```

Run the worker separately with the same connection and vault settings. Set `Knowledge__WriterEnabled=true` on the worker to process approved proposals. Without a configured vault, memory APIs still work while knowledge calls return 503 and the worker skips indexing.

Add migrations with:

```powershell
dotnet ef migrations add Name --project src/Brain.Infrastructure --output-dir Migrations
```

## Brain Client

`src/Brain.Client` builds a standalone `brain` executable for Windows, Linux, and macOS x64/ARM64:

```powershell
dotnet publish src/Brain.Client -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The installers accept a published binary and its expected SHA-256 digest. Configure an OIDC authority, audience, public client ID, subject-to-agent binding, and the required device permissions. The client supports `brain login`, harness connections, skill sync, diagnostics, foreground agent inventory/heartbeats, and local MCP proxying. Device credentials are stored in the operating system credential store. See [client onboarding](client-onboarding.md) for scope and known gaps.

## Operational limits

Registry discovery uses `Registry__RootPath` in the API and worker. Missing registry configuration disables that module. The sample registry and an operator-managed Git checkout are supported.

Tracing works without an external collector. `ActivityTracing__RetentionDays` defaults to 7; `ActivityTracing__OtlpEndpoint` enables optional OTLP/gRPC export. Authenticated operations return `X-Trace-Id` and continue incoming W3C `traceparent`. Development agents inspect their own traces; administrators need explicit system-wide grants. See [activity tracing](activity-tracing.md).

The built-in embedding provider uses deterministic 384-dimensional token hashing. It exercises pgvector without an API key but is not a learned semantic model and should not be treated as reliable paraphrase search. A real provider and explicit re-embedding are required for semantic recall.
