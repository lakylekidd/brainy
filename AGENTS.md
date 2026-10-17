# Agent Instructions


## Project - Homelab Brain

Homelab Brain is a self-hosted shared platform providing knowledge, memory, skills, tools, permissions, semantic search, and MCP access to AI agents.

It is independent of any specific AI harness. Codex, Claude Code, GitHub Copilot, Hermes, Paperclip, and future agents should all be able to work with the project.

## Before making changes
1. Read docs/architecture.md
2. Read docs/conventions.md

Always:
1. Inspect the current repository and Git status.
2. Read the documentation relevant to the task.
3. Follow existing architecture and conventions.
4. Prefer extending existing patterns over introducing new ones.
5. Implement the smallest complete change required.
6. Add or update tests.
7. Run relevant tests before finishing.
8. Update documentation when behavior or architecture changes.

Do not implement future functionality unless required by the current task.

## Documentation

Detailed project design lives in `docs/`.

Read the relevant documents before modifying their areas:

```text
docs/
├── architecture.md
├── conventions.md
├── foundation.md
├── knowledge.md
├── knowledge-writes.md
├── skills-and-tools.md
├── mcp.md
├── metadata-enrichment.md
└── verification.md
```

Use these documents as the source of truth for established architecture and milestone decisions.

Whenever a change affects behavior, architecture, APIs, configuration, setup, workflows, data models, or implementation decisions, update the relevant files in `docs/` and update `README.md` when the change affects project overview, setup, usage, prerequisites, or developer onboarding.

A task is not complete until the code, tests, README, and relevant documentation are consistent.

Do not duplicate detailed documentation into this file.

## Architecture Rules

The project is a modular monolith built primarily with:

- .NET 10 / ASP.NET Core
- Entity Framework Core
- PostgreSQL
- pgvector
- Docker / Docker Compose
- OpenAPI
- MCP

Use Clean Architecture principles where useful, but avoid unnecessary abstractions.

Typical dependency direction:

```text
Domain
  ↑
Application
  ↑
Infrastructure

Api    → Application
Mcp    → Application
Worker → Application
```

Business logic should live in Domain/Application, not be duplicated between API, MCP, Worker, or Infrastructure.

## Core Principles

Always preserve these distinctions:

```text
Brain ≠ Agent Harness
Knowledge ≠ Memory
Skills ≠ Tools
Permissions ≠ AI Decisions
Secrets ≠ Brain Data
Agent Proposals ≠ Direct Knowledge Writes
```

Important rules:

- Authorization must always be deterministic.
- Never let an LLM decide permissions.
- Canonical knowledge remains Markdown/filesystem/Git.
- PostgreSQL may index knowledge but is not its canonical source.
- Agents must not directly modify canonical knowledge files.
- Knowledge changes must use the proposal/revision-token/Brain Writer workflow.
- Never silently overwrite a newer document revision.
- Secrets remain in Infisical; never store raw secrets in the Brain.
- AI/provider integrations must use abstractions rather than coupling application logic to a specific provider.
- Prefer deterministic logic over LLM calls whenever possible.

## Coding Guidelines

Follow existing conventions in `docs/conventions.md`.

In general:

- Keep implementations simple and explicit.
- Prefer working vertical slices over speculative architecture.
- Avoid unnecessary dependencies.
- Use nullable reference types.
- Prefer async APIs for I/O.
- Propagate `CancellationToken` where appropriate.
- Keep public API contracts separate from persistence entities.
- Validate external and AI-generated input.
- Validate filesystem paths and prevent path traversal.
- Never log secrets.
- Do not bypass application services from API, MCP, or Worker code.

## Testing

Changes should include meaningful tests where appropriate.

Prefer:

- unit tests for Domain/Application behavior
- integration tests for PostgreSQL, EF Core, pgvector, APIs, Git/filesystem behavior, and concurrency

Do not consider a task complete if relevant tests are failing.

Use `docs/verification.md` for project-specific verification requirements.

## Working With Other Agents

Multiple coding agents may work on this repository.

Before editing:

- check the current branch and Git status
- re-read files before modifying them
- preserve unrelated changes
- do not reset, clean, revert, force-push, or discard another agent's work unless explicitly instructed

Agent-specific configuration may exist in `.github/`, `.claude/`, `.codex/`, or similar directories.

Those files may provide tool-specific behavior, but this `AGENTS.md` and the project documentation remain the shared repository-level instructions.

## When Unsure

Before inventing a new pattern:

1. Search the existing implementation.
2. Check the relevant `docs/` file.
3. Follow the established approach when one exists.
4. Only introduce a new architectural pattern when the existing design cannot reasonably support the requirement.

## Git
- Never commit directly to main.
- Use conventional commits.
- One logical change per commit.

## Skills
Reusable workflows are located in `.agents/skills/`.