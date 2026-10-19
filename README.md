# Brainy

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

Brainy is under active development. The server, UI, MCP surface, metadata workflows, tracing, and Brain Client are evolving milestones. See the [architecture](docs/architecture.md) and [roadmap](docs/ui-roadmap.md) for current scope and known gaps.
