<p align="center">
  <img src="docs/assets/brainy-logo.svg" alt="Brainy" width="144">
</p>

<h1 align="center">Brainy</h1>

<p align="center">
  A self-hosted shared workspace for AI agents.
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-PolyForm%20Noncommercial-orange.svg" alt="PolyForm Noncommercial License"></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=111827" alt="React 19">
  <img src="https://img.shields.io/badge/TypeScript-5.7-3178C6?logo=typescript&logoColor=white" alt="TypeScript">
  <img src="https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL 17">
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" alt="Docker Compose">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Vite-6-646CFF?logo=vite&logoColor=white" alt="Vite">
  <img src="https://img.shields.io/badge/D3.js-7-F9A03C?logo=d3.js&logoColor=white" alt="D3.js">
  <img src="https://img.shields.io/badge/Sigma.js-3-111827" alt="Sigma.js">
  <img src="https://img.shields.io/badge/EF%20Core-10.0-512BD4?logo=dotnet&logoColor=white" alt="Entity Framework Core">
  <img src="https://img.shields.io/badge/pgvector-PostgreSQL-4169E1?logo=postgresql&logoColor=white" alt="pgvector">
  <img src="https://img.shields.io/badge/Vitest-5-6E9F18?logo=vitest&logoColor=white" alt="Vitest">
  <img src="https://img.shields.io/badge/Playwright-E2E-2EAD33?logo=playwright&logoColor=white" alt="Playwright">
  <img src="https://img.shields.io/badge/MCP-Streamable%20HTTP-6B7280" alt="Streamable HTTP MCP">
</p>

Brainy is a self-hosted shared workspace for AI agents. It provides permission-aware knowledge, memory, skills, tools, search, and MCP access through one connected platform.

## Highlights

- Canonical Markdown knowledge with safe, reviewable change proposals
- Deterministic identity and permission management
- Scoped agent memory and unified search
- Permission-filtered skills, tools, and capabilities
- Streamable HTTP MCP access
- Optional device client for connecting agent harnesses
- A visual knowledge graph and operations UI

## Quick start

Requirements: PowerShell 7, Git, and Docker with Linux containers.

```powershell
.\scripts\Start-Development.ps1
```

Then open `http://localhost:5173` and use the UI administrator token from the generated `.env` file. Keep that token private.

## Documentation

- [Local setup and configuration](docs/setup.md)
- [Architecture](docs/architecture.md)
- [API and authentication](docs/api.md)
- [UI guide](docs/ui.md)
- [Brain Client onboarding](docs/client-onboarding.md)
- [MCP](docs/mcp.md)
- [Knowledge and safe writes](docs/knowledge.md) · [write workflow](docs/knowledge-writes.md)
- [Skills and tools](docs/skills-and-tools.md)
- [Development and verification](docs/development.md) · [verification reference](docs/verification.md)
- [Roadmap](docs/ui-roadmap.md)

## Project status

Brainy is under active development. The server, UI, MCP surface, metadata workflows, tracing, and Brain Client are evolving milestones. See the [architecture](docs/architecture.md) and [roadmap](docs/ui-roadmap.md) for current scope and known gaps. Brainy is licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE).
