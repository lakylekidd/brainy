# Graph Report - homelab-brain  (2026-10-19)

## Corpus Check
- 365 files · ~148,524 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 15 file(s) not represented in the graph (top: (none) 4, .css 4, .ttf 3)

## Summary
- 2804 nodes · 8743 edges · 133 communities (97 shown, 36 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 873 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7aa3b6b2`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- HttpClient
- Brain.Infrastructure.Migrations
- MetadataDocumentProcessor
- Guid
- storytelling.md
- GraphSource
- KnowledgeGraph.tsx
- PavDashboard.tsx
- VaultCanvas
- AgentContext
- career/index.md
- Brain.Infrastructure
- package.json
- Agents.tsx
- App.tsx
- RegistryService
- CommandHarnessAdapter
- client.ts
- ActivityEvent
- .Parse
- DeviceRuntime
- KnowledgeWriteProposal
- ActivityFilter
- Regex
- MetadataApiTests
- AgentMemory
- ActivitySanitizer
- publishing.md
- BrainDbContext
- planning.md
- BrainFixture
- system_text_json
- personal.md
- weekly-review.md
- Device
- ProvisioningPlan
- InlineData
- Fact
- FileSystemKnowledgeVault
- McpTests
- .AddBrainActivity
- .AddBrain
- KnowledgeProposalApiTests
- ProvisioningView
- Agent
- DeviceApiTests.cs
- IActivityPublisher
- Brain.Domain
- .Plan
- CompatibleMetadataProvider
- compilerOptions
- VaultSandbox
- BrainException
- RetainedActivityEvent
- ILogger
- KnowledgeDocument
- .Require
- ActivityApiTests.cs
- DateTimeOffset
- IReadOnlyDictionary
- .OnModelCreating
- Agent Instructions
- .ScanCoreAsync
- ActivityHarness
- MetadataScanState
- MemoryEmbedding
- McpRegistration.cs
- AgentAuthenticationHandler
- Brain UI roadmap
- Brain UI
- RegistryIndexEntry
- RetainedActivityEvent
- PavDashboard
- Architecture
- Implementation verification
- Test-Mcp.ps1
- ActivityEvent
- install-brain-client.sh
- vite-env.d.ts
- Vector
- Local setup
- Development and verification
- Dictionary
- README.md
- Safe knowledge writes — Milestone 3
- MigrationBuilder
- Foundation design — Milestone 1
- MacKeychain
- KnowledgeChunk
- .LoadAsync
- @playwright/test
- ApprovedPackage
- .InstallAsync
- SemanticMetadata
- .RunAsync
- .SyncAsync
- .Create
- KnowledgeOptions
- MetadataOptions
- Conventions
- MetadataInspection
- WriterLease
- preferences.md
- Knowledge — Milestone 2
- Vault metadata enrichment
- Brain MCP server
- VaultDocument
- ActivityTracing
- demo-vault/AGENTS.md
- API and authentication
- Proxmox nacht-02
- BrainClientOnboarding
- Business
- Content
- Skills, tools, and capability discovery
- agent-platform/README.md
- deploy-service/SKILL.md
- plan-deployment/SKILL.md
- Infrastructure/README.md
- Procedures/README.md
- Services/README.md

## God Nodes (most connected - your core abstractions)
1. `AgentContext` - 131 edges
2. `BrainException` - 110 edges
3. `BrainDbContext` - 105 edges
4. `Brain.Domain` - 84 edges
5. `Brain.Application` - 80 edges
6. `KnowledgeWriteProposal` - 68 edges
7. `AuditEvent` - 61 edges
8. `Brain.Infrastructure` - 61 edges
9. `AgentMemory` - 37 edges
10. `RegistryService` - 36 edges

## Surprising Connections (you probably didn't know these)
- `Operational constraints and limits` --references--> `IActivityPublisher`  [INFERRED]
  docs/architecture.md → src/Brain.Application/ActivityContracts.cs
- `Embeddings` --references--> `IEmbeddingProvider`  [INFERRED]
  docs/foundation.md → src/Brain.Application/Contracts.cs
- `Identity read contracts` --references--> `IAgentDirectory`  [INFERRED]
  docs/ui.md → src/Brain.Application/Contracts.cs
- `Worker and optional semantic provider` --references--> `IMetadataEnrichmentProvider`  [INFERRED]
  docs/metadata-enrichment.md → src/Brain.Application/MetadataContracts.cs
- `Unified search` --references--> `SearchService`  [INFERRED]
  docs/mcp.md → src/Brain.Application/SearchService.cs

## Import Cycles
- None detected.

## Communities (133 total, 36 thin omitted)

### Community 0 - "Task"
Cohesion: 0.06
Nodes (9): IKnowledgeProposalStore, KnowledgeWriterService, IMetadataScanStore, ProposalConcurrencyException, KnowledgeIndex, ScanLease, KnowledgeProposalStore, MetadataScanStore (+1 more)

### Community 1 - "HttpClient"
Cohesion: 0.21
Nodes (7): ClientState, ConnectedHarnesses, DirectoryPath, FilePath, HarnessConnections, Adapters, ClientProgram

### Community 3 - "MetadataDocumentProcessor"
Cohesion: 0.31
Nodes (4): MetadataPatch, MetadataDocumentProcessor, SchemaHash, MetadataTests

### Community 4 - "Guid"
Cohesion: 0.12
Nodes (11): DeviceCredential, DeviceHeartbeatRequest, DeviceRegistration, DeviceView, IDeviceStore, RegisterDeviceRequest, UpdateDeviceRequest, DeviceStore (+3 more)

### Community 5 - "storytelling.md"
Cohesion: 0.05
Nodes (45): Whiteboard, Filming session, References, Video storytelling, Community reply principles, Working references, Audience questions, Working references (+37 more)

### Community 6 - "GraphSource"
Cohesion: 0.10
Nodes (17): Retrieval and indexing, GraphEndpoints, GraphEntity, GraphEntityPage, GraphIds, GraphQuery, GraphReference, GraphRelationship (+9 more)

### Community 7 - "KnowledgeGraph.tsx"
Cohesion: 0.08
Nodes (30): sigma, canvasFont, canvasPalette(), emptyGraph, EntityType, entityTypes, GraphEntity, GraphRelationship (+22 more)

### Community 8 - "PavDashboard.tsx"
Cohesion: 0.08
Nodes (53): d3, demoGraph(), areaSectors(), createLayout(), Guide, Layout, nodePlacement(), Placement (+45 more)

### Community 9 - "VaultCanvas"
Cohesion: 0.17
Nodes (18): drawSignal(), flowProgress(), linkControl(), linkPoint(), Point, signalDuration(), signalPath(), defaultCamera() (+10 more)

### Community 10 - "AgentContext"
Cohesion: 0.11
Nodes (11): DeviceEndpoints, AgentView, CreateRoleRequest, IAgentDirectory, PermissionView, RoleView, AgentContext, AgentDirectory (+3 more)

### Community 11 - "career/index.md"
Cohesion: 0.05
Nodes (44): Interview story bank, Working references, Questions for a team, Working references, Resume editing notes, Working references, Thoughtful conversations, Feedback practice (+36 more)

### Community 12 - "Brain.Infrastructure"
Cohesion: 0.05
Nodes (36): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.AspNetCore.OpenApi (10.0.13), Microsoft.EntityFrameworkCore (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Microsoft.Extensions.Configuration.Binder (10.0.13) (+28 more)

### Community 13 - "package.json"
Cohesion: 0.05
Nodes (39): graphology, jsdom, react-dom, @testing-library/jest-dom, @types/d3, @types/react, @types/react-dom, typescript (+31 more)

### Community 14 - "Agents.tsx"
Cohesion: 0.08
Nodes (41): lucide-react, AgentView, apiBaseUrl, ApiError, asResource(), AuditEvent, Credentials, isUnauthorized() (+33 more)

### Community 15 - "App.tsx"
Cohesion: 0.09
Nodes (31): react, @testing-library/react, AgentProfile, authenticate(), credentials, Agents, App(), navigate() (+23 more)

### Community 16 - "RegistryService"
Cohesion: 0.06
Nodes (31): Canonical files and index, IAuditLog, CapabilitySearchResult, EmbeddedRegistryDefinition, IRegistryIndex, IRegistrySource, RegistryHit, RegistryIndexStatus (+23 more)

### Community 17 - "CommandHarnessAdapter"
Cohesion: 0.07
Nodes (29): API and audit, Brain Client and device onboarding, CLI and installation, Harness connections and skills, Identity and device credentials, Provisioning, Remaining Module 19 work, Runtime reports and dependencies (+21 more)

### Community 18 - "client.ts"
Cohesion: 0.17
Nodes (19): DashboardData, PageId, Today(), cappedNote(), count(), Dashboard(), DashboardProps, EmptyState() (+11 more)

### Community 19 - "ActivityEvent"
Cohesion: 0.20
Nodes (3): IActivityStore, TraceView, ActivityApiTests

### Community 20 - ".Parse"
Cohesion: 0.14
Nodes (3): FileSystemRegistrySource, RegistryOptions, RootPath

### Community 21 - "DeviceRuntime"
Cohesion: 0.13
Nodes (15): DeviceRuntime, Architecture, CommandsJson, DeviceId, DockerAvailable, EnvironmentId, Healthy, Kind (+7 more)

### Community 22 - "KnowledgeWriteProposal"
Cohesion: 0.06
Nodes (31): KnowledgeWriteProposal, AgentId, AppliedAt, BaseContentHash, Content, CreatedAt, EnrichmentConfidence, EnrichmentModel (+23 more)

### Community 23 - "ActivityFilter"
Cohesion: 0.15
Nodes (8): ActivityEndpoints, ActivityParameters, RequestAgent, Context, DeviceId, ActivityFilter, ActivityQueryService, IActivitySubscription

### Community 25 - "MetadataApiTests"
Cohesion: 0.17
Nodes (8): KnowledgeProposalPreview, MetadataIssueView, FakeProvider, Calls, DuringCall, Info, Model, MetadataApiTests

### Community 26 - "AgentMemory"
Cohesion: 0.06
Nodes (36): AgentMemory, AgentId, Confidence, Content, CreatedAt, ExpiresAt, Id, Importance (+28 more)

### Community 27 - "ActivitySanitizer"
Cohesion: 0.20
Nodes (3): BrainActivityPublisher, ActivitySanitizer, SanitizingActivityProcessor

### Community 28 - "publishing.md"
Cohesion: 0.06
Nodes (41): Publishing queue, Distribution choices, Working references, Example collection, Working references, Opening experiments, Working references, Revision log (+33 more)

### Community 29 - "BrainDbContext"
Cohesion: 0.07
Nodes (27): Permission, Description, Id, Role, Description, Id, Permissions, BrainDbContext (+19 more)

### Community 30 - "planning.md"
Cohesion: 0.06
Nodes (40): Atlas website, Client feedback log, Working references, Client review agenda, Working references, Client success measures, Working references, Studio capacity (+32 more)

### Community 31 - "BrainFixture"
Cohesion: 0.08
Nodes (9): UI admin login and Docker development stack (17 October 2026), AgentApiTests, BrainFixture, ConnectionString, Factory, RegistryPath, VaultPath, DeviceApiTests (+1 more)

### Community 33 - "personal.md"
Cohesion: 0.06
Nodes (40): Application map, Attention boundaries, Working references, Energy patterns, Working references, Evening reset, Working references, Simple weekly menu (+32 more)

### Community 34 - "weekly-review.md"
Cohesion: 0.08
Nodes (25): Weekly review, Archive policy, Working references, Capture inbox, Working references, File naming, Working references, Reading the graph (+17 more)

### Community 35 - "Device"
Cohesion: 0.08
Nodes (24): Device, AgentId, Architecture, ClientVersion, CredentialHash, DisplayName, Hostname, Id (+16 more)

### Community 36 - "ProvisioningPlan"
Cohesion: 0.08
Nodes (24): ProvisioningPlan, ApprovedAt, ApprovedBy, Architecture, CompletedAt, CreatedAt, DeviceId, Id (+16 more)

### Community 37 - "InlineData"
Cohesion: 0.10
Nodes (6): Program, ApiContractTests, ContractFactory, AdminTokenHash, BrainFactory, RegistrySourceTests

### Community 38 - "Fact"
Cohesion: 0.15
Nodes (5): CreateAgentRequest, RememberRequest, UpdateAccessRequest, GraphApiTests, MemoryApiTests

### Community 40 - "McpTests"
Cohesion: 0.19
Nodes (5): MemorySearchResult, MemoryView, BrainSearchResult, MemorySearchResponse, McpTests

### Community 41 - ".AddBrainActivity"
Cohesion: 0.12
Nodes (13): ActivityTracingOptions, CleanupIntervalSeconds, OtlpEndpoint, QueueCapacity, RetentionDays, SubscriberCapacity, IActivityStream, ActivityLiveBus (+5 more)

### Community 42 - ".AddBrain"
Cohesion: 0.14
Nodes (12): DependencyService, DeviceSkillReport, DeviceSkillService, DeviceSkillSyncRequest, DeviceSkillView, IDeviceSkillStore, IRuntimeStore, RuntimeReportRequest (+4 more)

### Community 43 - "KnowledgeProposalApiTests"
Cohesion: 0.28
Nodes (7): ICanonicalKnowledgeWriter, KnowledgeProposalView, Type, KnowledgeWriterResult, ProposeKnowledgeChangeRequest, ReviewKnowledgeProposalRequest, KnowledgeProposalApiTests

### Community 44 - "ProvisioningView"
Cohesion: 0.13
Nodes (8): DeviceService, IProvisioningStore, ProvisioningService, ProvisioningView, ProvisionResultRequest, BrainException, StatusCode, ProvisioningStore

### Community 45 - "Agent"
Cohesion: 0.15
Nodes (11): Agent, CreatedAt, Description, Enabled, Id, MetadataJson, Name, Permissions (+3 more)

### Community 47 - "IActivityPublisher"
Cohesion: 0.13
Nodes (12): Activity tracing and live events, Instrumentation and boundaries, Live API, Retained inspection and authorization, Safety, configuration and operations, Verification, ActivityOperation, ActivityPublisherExtensions (+4 more)

### Community 48 - "Brain.Domain"
Cohesion: 0.10
Nodes (7): Brain.Infrastructure, Brain.UnitTests, Brain.Domain, Brain.Tests, Brain.Application, DependencyView, ProvisionRequest

### Community 49 - ".Plan"
Cohesion: 0.15
Nodes (12): KnowledgeOperation, Append, CreateDocument, MetadataEnrichment, ReplaceSection, KnowledgeProposalStatus, Applied, Approved (+4 more)

### Community 50 - "CompatibleMetadataProvider"
Cohesion: 0.13
Nodes (12): IMetadataEnrichmentProvider, Info, MetadataEnrichmentInput, MetadataProviderInfo, CompatibleMetadataProvider, Info, MetadataProviderOptions, ApiKey (+4 more)

### Community 51 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 52 - "VaultSandbox"
Cohesion: 0.19
Nodes (6): Tests and documentation, KnowledgeTests, KnowledgeWriteTests, TestGit, VaultSandbox, Root

### Community 53 - "BrainException"
Cohesion: 0.28
Nodes (3): PreparedKnowledgeWrite, GitKnowledgeWriter, GitResult

### Community 54 - "RetainedActivityEvent"
Cohesion: 0.11
Nodes (18): ActivityStatus, Cancelled, Failed, Running, Started, Succeeded, ActivityTrace, ActiveRoots (+10 more)

### Community 55 - "ILogger"
Cohesion: 0.33
Nodes (8): ActivityDispatchService, ActivityRetentionCleanup, DatabaseStartup, ExpiredMemoryCleanup, KnowledgeIndexing, KnowledgeWriteProcessing, MetadataScanning, RegistryIndexing

### Community 56 - "KnowledgeDocument"
Cohesion: 0.07
Nodes (33): ChunkedMarkdown, EmbeddedKnowledgeChunk, IKnowledgeIndex, IKnowledgeVault, IMarkdownChunker, Version, KnowledgeDocumentView, KnowledgeResultMetadata (+25 more)

### Community 57 - ".Require"
Cohesion: 0.23
Nodes (5): Identity, validation and errors, MemoryService, BrainSearchRequest, Validation, Permissions

### Community 59 - "DateTimeOffset"
Cohesion: 0.07
Nodes (27): EmbeddingModel, IEmbeddingProvider, Model, IMemoryStore, MemorySearchRequest, ScoredMemory, AuditEvent, Action (+19 more)

### Community 60 - "IReadOnlyDictionary"
Cohesion: 0.24
Nodes (4): Provisioner, RuntimeInventory, RuntimeReport, RuntimeSnapshot

### Community 61 - ".OnModelCreating"
Cohesion: 0.15
Nodes (11): DeviceSkill, DeviceId, Harness, SkillId, SourceHash, Status, UpdatedAt, DeviceSkillStatus (+3 more)

### Community 62 - "Agent Instructions"
Cohesion: 0.13
Nodes (14): Agent Instructions, Architecture Rules, Before making changes, Codebase Knowledge, Coding Guidelines, Core Principles, Documentation, Git (+6 more)

### Community 63 - ".ScanCoreAsync"
Cohesion: 0.23
Nodes (3): KnowledgeProposalService, MetadataScanResult, MetadataService

### Community 64 - "ActivityHarness"
Cohesion: 0.19
Nodes (6): ActivityHarness, Publisher, Queue, ActivityTests, RecordingExporter, Spans

### Community 65 - "MetadataScanState"
Cohesion: 0.14
Nodes (13): MetadataScanState, CheckedAt, ContentHash, IssuesJson, LastEnrichedAt, LastError, Model, Path (+5 more)

### Community 66 - "MemoryEmbedding"
Cohesion: 0.29
Nodes (7): MemoryEmbedding, Dimensions, Embedding, Memory, MemoryId, Model, Provider

### Community 68 - "AgentAuthenticationHandler"
Cohesion: 0.17
Nodes (5): AgentAuthenticationHandler, AgentAuthenticationOptions, AgentTokenHashes, UiAdminAgentId, UiAdminTokenHash

### Community 69 - "Brain UI roadmap"
Cohesion: 0.15
Nodes (13): Brain UI roadmap, Milestone 10 — Live Activity Graph, Milestone 2 — Knowledge Graph, Milestone 3 — WebObsidian Integration, Milestone 4 — Agents + Permissions, Milestone 5 — Skills + Tools, Milestone 6 — Connections, Milestone 7 — Proposals + Metadata (+5 more)

### Community 70 - "Brain UI"
Cohesion: 0.12
Nodes (16): Agents and permissions, Appearance, Authentication and safety, Brain UI, Current scope: UI Milestones 1-4, End-to-end verification, Existing API coverage and gaps, Graph contracts and authorization (+8 more)

### Community 71 - "RegistryIndexEntry"
Cohesion: 0.11
Nodes (19): RegistryIndexEntry, Capabilities, ContentHash, Description, Embedding, EmbeddingDimensions, EmbeddingModel, EmbeddingProvider (+11 more)

### Community 72 - "RetainedActivityEvent"
Cohesion: 0.12
Nodes (17): RetainedActivityEvent, Action, AgentId, DurationMs, ErrorMessage, ErrorType, EventType, Id (+9 more)

### Community 73 - "PavDashboard"
Cohesion: 0.22
Nodes (11): formatElapsed(), PavDashboard(), chooseFile(), clearSelection(), closeDetails(), finishRoutinePreview(), selectEffortAtPointer(), selectModelEffortAtPointer() (+3 more)

### Community 74 - "Architecture"
Cohesion: 0.12
Nodes (14): Architecture, Code organization, Data ownership and persistence, Identity and request flow, Metadata enrichment, Operational constraints and limits, Purpose and boundaries, Reviewed canonical changes (+6 more)

### Community 75 - "Implementation verification"
Cohesion: 0.13
Nodes (15): Brain Client and device onboarding, partial Module 19 (18 October 2026), Graph layout refinement (17 October 2026), Implementation verification, Initial Docker failure history — 16 October 2026, PAV OS clickable ring focus (18 October 2026), PAV OS demo dashboard (18 October 2026), PAV OS file-backed area hubs and dense clusters (18 October 2026), PAV OS full-width graph and reference motion (18 October 2026) (+7 more)

### Community 78 - "ActivityEvent"
Cohesion: 0.19
Nodes (4): ActivityEvent, ActivityFiltering, Subscription, ActivityStore

### Community 88 - "Vector"
Cohesion: 0.19
Nodes (3): InitialFoundation, KnowledgeIndex, BrainDbContextModelSnapshot

### Community 89 - "Local setup"
Cohesion: 0.29
Nodes (7): Frontend hot reload, Local setup, Production boundary, Requirements, Services and ports, Start the stack, Vault and development data

### Community 90 - "Development and verification"
Cohesion: 0.40
Nodes (5): Brain Client, Build and test, Development and verification, Host-based development, Operational limits

### Community 92 - "README.md"
Cohesion: 0.30
Nodes (4): Documentation, Highlights, Project status, Quick start

### Community 93 - "Safe knowledge writes — Milestone 3"
Cohesion: 0.17
Nodes (9): Audit and verification, Boundaries and schema, Dedicated repository and writer configuration, Durable publication and recovery, HTTP flow, Lifecycle and permissions, Operations, Safe knowledge writes — Milestone 3 (+1 more)

### Community 94 - "MigrationBuilder"
Cohesion: 0.24
Nodes (3): SkillsAndTools, MetadataEnrichment, KnowledgeGraph

### Community 95 - "Foundation design — Milestone 1"
Cohesion: 0.25
Nodes (8): Architectural concerns, Audit and worker, Embeddings, Foundation design — Milestone 1, Identity and permissions, Initial PostgreSQL schema, Scope and plan, Structure and boundaries

### Community 97 - "KnowledgeChunk"
Cohesion: 0.20
Nodes (10): KnowledgeChunk, Content, Document, DocumentId, Embedding, EndLine, Heading, Id (+2 more)

### Community 98 - ".LoadAsync"
Cohesion: 0.31
Nodes (3): SecureCredentialStore, Service, McpProxy

### Community 100 - "ApprovedPackage"
Cohesion: 0.18
Nodes (10): ApprovedPackage, Architecture, OperatingSystem, PackageId, Requirement, Sha256, Version, WheelUrl (+2 more)

### Community 102 - "SemanticMetadata"
Cohesion: 0.18
Nodes (11): SemanticMetadata, Confidence, Domains, Environment, Projects, RelatedConcepts, Services, Summary (+3 more)

### Community 104 - ".SyncAsync"
Cohesion: 0.47
Nodes (3): SkillSynchronizer, Root, SkillSyncState

### Community 106 - "KnowledgeOptions"
Cohesion: 0.27
Nodes (7): DocumentProvenance, IKnowledgeProvenance, KnowledgeOptions, MaximumDocumentBytes, VaultPath, WriterEnabled, GitKnowledgeProvenance

### Community 107 - "MetadataOptions"
Cohesion: 0.24
Nodes (7): MetadataJson, MetadataOptions, AgentId, DocumentTypes, Enabled, SchemaVersion, SemanticEnabled

### Community 109 - "Conventions"
Cohesion: 0.25
Nodes (8): C# style, Canonical files, registry and metadata, Configuration and secrets, Conventions, Names and wire formats, Persistence, concurrency and audit, Toolchain and local workflow, RegistryAccess

### Community 110 - "MetadataInspection"
Cohesion: 0.36
Nodes (3): MetadataInspection, MetadataIssue, Parsed

### Community 112 - "preferences.md"
Cohesion: 0.25
Nodes (4): Working preferences, Personal compass, Reading shelf, An offline weekend

### Community 113 - "Knowledge — Milestone 2"
Cohesion: 0.29
Nodes (7): Boundaries, Chunks and revisions, Configuration and operation, Knowledge — Milestone 2, Paths and safety, Read and search, Schema and audit

### Community 114 - "Vault metadata enrichment"
Cohesion: 0.29
Nodes (7): APIs, Generation and preservation, Scan state and review, Schema, Vault metadata enrichment, Verification, Worker and optional semantic provider

### Community 115 - "Brain MCP server"
Cohesion: 0.33
Nodes (6): Brain MCP server, Connect an agent, Errors and audit, Tools, Unified search, Verify

### Community 116 - "VaultDocument"
Cohesion: 0.47
Nodes (3): VaultDocument, IMetadataDocumentProcessor, SchemaHash

### Community 118 - "demo-vault/AGENTS.md"
Cohesion: 0.40
Nodes (4): Areas of interest, Context and applications, Operating rhythm, Shared capabilities

### Community 119 - "API and authentication"
Cohesion: 0.50
Nodes (4): API and authentication, Authentication, Endpoint catalog, Example request

### Community 120 - "Proxmox nacht-02"
Cohesion: 0.50
Nodes (3): Change coordination, Cluster recovery, Proxmox nacht-02

### Community 122 - "Business"
Cohesion: 0.50
Nodes (4): Business, Capabilities, Field notes and projects, Starting points

### Community 123 - "Content"
Cohesion: 0.50
Nodes (4): Capabilities, Content, Field notes and projects, Starting points

### Community 124 - "Skills, tools, and capability discovery"
Cohesion: 0.67
Nodes (3): Authorization, HTTP API, Skills, tools, and capability discovery

## Knowledge Gaps
- **407 isolated node(s):** `Context`, `DeviceId`, `AgentTokenHashes`, `UiAdminAgentId`, `UiAdminTokenHash` (+402 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 887 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **36 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Brain UI` connect `Brain UI` to `README.md`?**
  _High betweenness centrality (0.129) - this node is a cross-community bridge._
- **Are the 6 inferred relationships involving `AgentContext` (e.g. with `Identity, validation and errors` and `.HandleAuthenticateAsync()`) actually correct?**
  _`AgentContext` has 6 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Context`, `DeviceId`, `AgentTokenHashes` to the rest of the system?**
  _407 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.06147732585320243 - nodes in this community are weakly interconnected._
- **Should `Guid` be split into smaller, more focused modules?**
  _Cohesion score 0.12395929694727105 - nodes in this community are weakly interconnected._
- **Should `storytelling.md` be split into smaller, more focused modules?**
  _Cohesion score 0.05328218243819267 - nodes in this community are weakly interconnected._
- **Should `GraphSource` be split into smaller, more focused modules?**
  _Cohesion score 0.09701928696668614 - nodes in this community are weakly interconnected._