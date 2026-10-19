# Graph Report - homelab-brain  (2026-10-19)

## Corpus Check
- 365 files · ~148,524 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 15 file(s) not represented in the graph (top: (none) 4, .css 4, .ttf 3)

## Summary
- 2326 nodes · 7819 edges · 106 communities (76 shown, 30 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 818 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7aa3b6b2`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- HttpClient
- DateTimeOffset
- JsonElement
- Guid
- KnowledgeDocument
- GraphSource
- KnowledgeGraph.tsx
- PavDashboard.tsx
- VaultCanvas
- AgentContext
- Brain.Domain
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
- devDependencies
- BrainDbContext
- Fact
- BrainFixture
- system_text_json
- BrainTools
- RegistryApiTests
- Device
- ProvisioningPlan
- InlineData
- .Agent
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
- ActivityApiTests.cs
- compilerOptions
- VaultSandbox
- BrainException
- RetainedActivityEvent
- ILogger
- KnowledgeApiTests
- .Require
- .Require
- IReadOnlyDictionary
- .OnModelCreating
- Agent Instructions
- KnowledgeProposalService
- ActivityHarness
- MetadataScanState
- MemoryEmbedding
- McpRegistration.cs
- AgentAuthenticationHandler
- Brain UI roadmap
- Brain UI
- VaultSandbox
- EmbeddingModel
- PavDashboard
- Architecture
- system_diagnostics
- Test-Mcp.ps1
- ActivityEvent
- install-brain-client.sh
- vite-env.d.ts
- DevelopmentEmbeddingProvider
- Local setup
- Development and verification
- Dictionary
- README.md
- MemoryType
- Knowledge graph
- Foundation design — Milestone 1
- MacKeychain
- KnowledgeChunk
- .LoadAsync
- @playwright/test
- KnowledgeContracts.cs
- .InstallAsync
- .RunAsync
- .SyncAsync
- .Create

## God Nodes (most connected - your core abstractions)
1. `AgentContext` - 130 edges
2. `BrainException` - 109 edges
3. `BrainDbContext` - 99 edges
4. `Brain.Domain` - 84 edges
5. `Brain.Application` - 80 edges
6. `KnowledgeWriteProposal` - 68 edges
7. `Brain.Infrastructure` - 61 edges
8. `AuditEvent` - 56 edges
9. `RegistryService` - 35 edges
10. `KnowledgeDocument` - 34 edges

## Surprising Connections (you probably didn't know these)
- `Data ownership and persistence` --references--> `BrainDbContext`  [INFERRED]
  docs/architecture.md → src/Brain.Infrastructure/BrainDbContext.cs
- `Operational constraints and limits` --references--> `IActivityPublisher`  [INFERRED]
  docs/architecture.md → src/Brain.Application/ActivityContracts.cs
- `Embeddings` --references--> `IEmbeddingProvider`  [INFERRED]
  docs/foundation.md → src/Brain.Application/Contracts.cs
- `Identity read contracts` --references--> `IAgentDirectory`  [INFERRED]
  docs/ui.md → src/Brain.Application/Contracts.cs
- `Identity and request flow` --references--> `IAgentDirectory`  [INFERRED]
  docs/architecture.md → src/Brain.Application/Contracts.cs

## Import Cycles
- None detected.

## Communities (106 total, 30 thin omitted)

### Community 0 - "Task"
Cohesion: 0.05
Nodes (21): KnowledgeDocumentView, IMetadataScanStore, IHarnessAdapter, Name, AuditEvent, Action, AgentId, Id (+13 more)

### Community 1 - "HttpClient"
Cohesion: 0.21
Nodes (7): ClientState, ConnectedHarnesses, DirectoryPath, FilePath, HarnessConnections, Adapters, ClientProgram

### Community 2 - "DateTimeOffset"
Cohesion: 0.06
Nodes (29): Brain.Infrastructure.Migrations, InitialFoundation, KnowledgeIndex, SafeKnowledgeWrites, SkillsAndTools, MetadataEnrichment, ActivityTracing, KnowledgeGraph (+21 more)

### Community 3 - "JsonElement"
Cohesion: 0.06
Nodes (41): VaultDocument, DocumentProvenance, IMetadataDocumentProcessor, SchemaHash, IMetadataEnrichmentProvider, Info, MetadataEnrichmentInput, MetadataInspection (+33 more)

### Community 4 - "Guid"
Cohesion: 0.12
Nodes (10): DeviceCredential, DeviceHeartbeatRequest, DeviceRegistration, DeviceView, IDeviceStore, RegisterDeviceRequest, DeviceService, DeviceSkillService (+2 more)

### Community 5 - "KnowledgeDocument"
Cohesion: 0.12
Nodes (14): KnowledgeDocument, ChunkerVersion, ContentHash, EmbeddingDimensions, EmbeddingModel, EmbeddingProvider, GraphReferences, GraphVersion (+6 more)

### Community 6 - "GraphSource"
Cohesion: 0.08
Nodes (19): GraphEndpoints, GraphEntity, GraphEntityPage, GraphIds, GraphQuery, GraphReference, GraphRelationship, GraphResult (+11 more)

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
Nodes (7): DeviceEndpoints, AgentView, IAgentDirectory, AgentDirectory, ContractDirectory, DeviceClientTests, DirectoryStub

### Community 12 - "Brain.Infrastructure"
Cohesion: 0.07
Nodes (39): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.AspNetCore.OpenApi (10.0.13), Microsoft.EntityFrameworkCore (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Microsoft.Extensions.Configuration.Binder (10.0.13) (+31 more)

### Community 13 - "package.json"
Cohesion: 0.07
Nodes (27): graphology, jsdom, react-dom, @testing-library/jest-dom, @types/d3, @types/react, @types/react-dom, typescript (+19 more)

### Community 14 - "Agents.tsx"
Cohesion: 0.08
Nodes (41): lucide-react, AgentView, apiBaseUrl, ApiError, asResource(), AuditEvent, Credentials, isUnauthorized() (+33 more)

### Community 15 - "App.tsx"
Cohesion: 0.09
Nodes (31): react, @testing-library/react, AgentProfile, authenticate(), credentials, Agents, App(), navigate() (+23 more)

### Community 16 - "RegistryService"
Cohesion: 0.13
Nodes (9): CreateRoleRequest, PermissionView, RoleView, RegistrySearchRequest, RegistryService, AgentContext, RegistryKind, Skill (+1 more)

### Community 17 - "CommandHarnessAdapter"
Cohesion: 0.10
Nodes (19): ClaudeCodeAdapter, CheckArguments, RemoveArguments, CodexAdapter, CheckArguments, RemoveArguments, CommandHarnessAdapter, CheckArguments (+11 more)

### Community 18 - "client.ts"
Cohesion: 0.17
Nodes (19): DashboardData, PageId, Today(), cappedNote(), count(), Dashboard(), DashboardProps, EmptyState() (+11 more)

### Community 20 - ".Parse"
Cohesion: 0.12
Nodes (4): FileSystemRegistrySource, RegistryOptions, RootPath, RegistrySourceTests

### Community 21 - "DeviceRuntime"
Cohesion: 0.12
Nodes (15): DeviceRuntime, Architecture, CommandsJson, DeviceId, DockerAvailable, EnvironmentId, Healthy, Kind (+7 more)

### Community 22 - "KnowledgeWriteProposal"
Cohesion: 0.06
Nodes (31): KnowledgeWriteProposal, AgentId, AppliedAt, BaseContentHash, Content, CreatedAt, EnrichmentConfidence, EnrichmentModel (+23 more)

### Community 23 - "ActivityFilter"
Cohesion: 0.14
Nodes (10): ActivityEndpoints, ActivityParameters, RequestAgent, Context, DeviceId, ActivityFilter, ActivityQueryService, IActivitySubscription (+2 more)

### Community 25 - "MetadataApiTests"
Cohesion: 0.17
Nodes (9): KnowledgeProposalPreview, MetadataIssueView, MetadataScanResult, FakeProvider, Calls, DuringCall, Info, Model (+1 more)

### Community 26 - "AgentMemory"
Cohesion: 0.07
Nodes (28): AgentMemory, AgentId, Confidence, Content, CreatedAt, ExpiresAt, Id, Importance (+20 more)

### Community 27 - "ActivitySanitizer"
Cohesion: 0.14
Nodes (4): BrainActivityPublisher, OutboundActivityObserver, ActivitySanitizer, SanitizingActivityProcessor

### Community 28 - "devDependencies"
Cohesion: 0.17
Nodes (12): devDependencies, jsdom, @playwright/test, @testing-library/jest-dom, @testing-library/react, @types/d3, @types/react, @types/react-dom (+4 more)

### Community 29 - "BrainDbContext"
Cohesion: 0.08
Nodes (21): BrainDbContext, ActivityEvents, ActivityTraces, Agents, AuditEvents, DeviceRuntimes, Devices, DeviceSkills (+13 more)

### Community 30 - "Fact"
Cohesion: 0.21
Nodes (5): MemorySearchRequest, MemorySearchResult, MemoryView, RememberRequest, MemoryApiTests

### Community 31 - "BrainFixture"
Cohesion: 0.06
Nodes (11): End-to-end verification, CreateAgentRequest, DatabaseInitializer, AgentApiTests, BrainFixture, ConnectionString, Factory, RegistryPath (+3 more)

### Community 34 - "RegistryApiTests"
Cohesion: 0.17
Nodes (13): CapabilitySearchResult, EmbeddedRegistryDefinition, IRegistryIndex, IRegistrySource, RegistryMetadata, RegistryResult, RegistryScanResult, SkillView (+5 more)

### Community 35 - "Device"
Cohesion: 0.08
Nodes (24): Device, AgentId, Architecture, ClientVersion, CredentialHash, DisplayName, Hostname, Id (+16 more)

### Community 36 - "ProvisioningPlan"
Cohesion: 0.08
Nodes (24): ProvisioningPlan, ApprovedAt, ApprovedBy, Architecture, CompletedAt, CreatedAt, DeviceId, Id (+16 more)

### Community 37 - "InlineData"
Cohesion: 0.11
Nodes (5): Program, ApiContractTests, ContractFactory, AdminTokenHash, BrainFactory

### Community 39 - "FileSystemKnowledgeVault"
Cohesion: 0.26
Nodes (6): FileSystemKnowledgeVault, KnowledgeOptions, MaximumDocumentBytes, VaultPath, WriterEnabled, GitKnowledgeProvenance

### Community 40 - "McpTests"
Cohesion: 0.18
Nodes (5): BrainSearchRequest, BrainSearchResult, SearchSourceFailure, MemorySearchResponse, McpTests

### Community 41 - ".AddBrainActivity"
Cohesion: 0.12
Nodes (13): ActivityTracingOptions, CleanupIntervalSeconds, OtlpEndpoint, QueueCapacity, RetentionDays, SubscriberCapacity, IActivityStream, ActivityLiveBus (+5 more)

### Community 42 - ".AddBrain"
Cohesion: 0.20
Nodes (18): Metadata enrichment, IAuditLog, IEmbeddingProvider, Model, IMemoryStore, IDeviceSkillStore, IKnowledgeVault, IMarkdownChunker (+10 more)

### Community 43 - "KnowledgeProposalApiTests"
Cohesion: 0.26
Nodes (8): ICanonicalKnowledgeWriter, KnowledgeProposalView, Type, KnowledgeWriterResult, ProposeKnowledgeChangeRequest, ReviewKnowledgeProposalRequest, KnowledgeWriterService, KnowledgeProposalApiTests

### Community 44 - "ProvisioningView"
Cohesion: 0.11
Nodes (11): DependencyService, IProvisioningStore, ProvisioningService, ProvisioningView, ProvisionResultRequest, RuntimeReportRequest, RuntimeService, RuntimeView (+3 more)

### Community 45 - "Agent"
Cohesion: 0.08
Nodes (28): ApprovedPackage, Architecture, OperatingSystem, PackageId, Requirement, Sha256, Version, WheelUrl (+20 more)

### Community 47 - "IActivityPublisher"
Cohesion: 0.22
Nodes (6): ActivityOperation, ActivityPublisherExtensions, IActivityPublisher, IActivityScope, Scope, WorkerActivity

### Community 48 - "Brain.Domain"
Cohesion: 0.12
Nodes (8): Brain.Domain, Brain.Application, DependencyView, UpdateDeviceRequest, DeviceSkillReport, DeviceSkillSyncRequest, ProvisionRequest, RegistryAccess

### Community 49 - ".Plan"
Cohesion: 0.12
Nodes (12): KnowledgeOperation, Append, CreateDocument, MetadataEnrichment, ReplaceSection, KnowledgeProposalStatus, Applied, Approved (+4 more)

### Community 51 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 53 - "BrainException"
Cohesion: 0.28
Nodes (3): PreparedKnowledgeWrite, GitKnowledgeWriter, GitResult

### Community 54 - "RetainedActivityEvent"
Cohesion: 0.06
Nodes (35): ActivityStatus, Cancelled, Failed, Running, Started, Succeeded, ActivityTrace, ActiveRoots (+27 more)

### Community 55 - "ILogger"
Cohesion: 0.27
Nodes (7): ActivityRetentionCleanup, DatabaseStartup, ExpiredMemoryCleanup, KnowledgeIndexing, KnowledgeWriteProcessing, MetadataScanning, RegistryIndexing

### Community 56 - "KnowledgeApiTests"
Cohesion: 0.24
Nodes (4): IKnowledgeIndex, MarkdownChunker, Version, KnowledgeApiTests

### Community 57 - ".Require"
Cohesion: 0.26
Nodes (3): KnowledgeSearchRequest, KnowledgeSearchResult, Validation

### Community 59 - ".Require"
Cohesion: 0.23
Nodes (9): FailingEmbedding, Model, InvalidEmbedding, Model, MemoryServiceTests, RecordingAudit, RecordingStore, Audit (+1 more)

### Community 60 - "IReadOnlyDictionary"
Cohesion: 0.22
Nodes (5): Provisioner, RuntimeInventory, RuntimeReport, RuntimeSnapshot, Permissions

### Community 61 - ".OnModelCreating"
Cohesion: 0.14
Nodes (11): DeviceSkill, DeviceId, Harness, SkillId, SourceHash, Status, UpdatedAt, DeviceSkillStatus (+3 more)

### Community 62 - "Agent Instructions"
Cohesion: 0.13
Nodes (14): Agent Instructions, Architecture Rules, Before making changes, Codebase Knowledge, Coding Guidelines, Core Principles, Documentation, Git (+6 more)

### Community 63 - "KnowledgeProposalService"
Cohesion: 0.11
Nodes (6): IKnowledgeProposalStore, KnowledgeProposalService, ProposalConcurrencyException, KnowledgeProposalStore, WriterLease, FailAppliedStore

### Community 64 - "ActivityHarness"
Cohesion: 0.18
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
Cohesion: 0.18
Nodes (11): Agents and permissions, Appearance, Authentication and safety, Brain UI, Current scope: UI Milestones 1-4, Existing API coverage and gaps, Identity read contracts, PAV OS demo dashboard (+3 more)

### Community 71 - "VaultSandbox"
Cohesion: 0.26
Nodes (3): KnowledgeTests, VaultSandbox, Root

### Community 72 - "EmbeddingModel"
Cohesion: 0.15
Nodes (5): EmbeddingModel, ScoredMemory, RegistryHit, RegistryIndexStatus, MemoryStore

### Community 73 - "PavDashboard"
Cohesion: 0.22
Nodes (11): formatElapsed(), PavDashboard(), chooseFile(), clearSelection(), closeDetails(), finishRoutinePreview(), selectEffortAtPointer(), selectModelEffortAtPointer() (+3 more)

### Community 74 - "Architecture"
Cohesion: 0.12
Nodes (13): Architecture, Code organization, Data ownership and persistence, Identity and request flow, Operational constraints and limits, Purpose and boundaries, Retrieval and indexing, Reviewed canonical changes (+5 more)

### Community 78 - "ActivityEvent"
Cohesion: 0.14
Nodes (3): ActivityEvent, ActivityFiltering, Subscription

### Community 88 - "DevelopmentEmbeddingProvider"
Cohesion: 0.32
Nodes (4): DevelopmentEmbeddingProvider, Model, MutatingProvider, Model

### Community 89 - "Local setup"
Cohesion: 0.29
Nodes (7): Frontend hot reload, Local setup, Production boundary, Requirements, Services and ports, Start the stack, Vault and development data

### Community 90 - "Development and verification"
Cohesion: 0.33
Nodes (5): Brain Client, Build and test, Development and verification, Host-based development, Operational limits

### Community 92 - "README.md"
Cohesion: 0.20
Nodes (8): API and authentication, Authentication, Endpoint catalog, Example request, Documentation, Highlights, Project status, Quick start

### Community 93 - "MemoryType"
Cohesion: 0.25
Nodes (8): MemoryType, Decision, Discovery, Fact, Incident, Observation, Preference, TaskContext

### Community 94 - "Knowledge graph"
Cohesion: 0.50
Nodes (4): Graph contracts and authorization, Knowledge graph, Sources, relationships and synchronization, label()

### Community 95 - "Foundation design — Milestone 1"
Cohesion: 0.17
Nodes (8): Architectural concerns, Audit and worker, Embeddings, Foundation design — Milestone 1, Identity and permissions, Initial PostgreSQL schema, Scope and plan, Structure and boundaries

### Community 97 - "KnowledgeChunk"
Cohesion: 0.20
Nodes (10): KnowledgeChunk, Content, Document, DocumentId, Embedding, EndLine, Heading, Id (+2 more)

### Community 98 - ".LoadAsync"
Cohesion: 0.31
Nodes (3): SecureCredentialStore, Service, McpProxy

### Community 100 - "KnowledgeContracts.cs"
Cohesion: 0.36
Nodes (6): ChunkedMarkdown, EmbeddedKnowledgeChunk, KnowledgeResultMetadata, KnowledgeScanFailure, KnowledgeScanResult, MarkdownChunk

### Community 104 - ".SyncAsync"
Cohesion: 0.47
Nodes (3): SkillSynchronizer, Root, SkillSyncState

## Knowledge Gaps
- **400 isolated node(s):** `Model`, `Id`, `Name`, `Description`, `TeamId` (+395 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 677 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **30 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Brain UI` connect `Brain UI` to `BrainFixture`, `Knowledge graph`, `Foundation design — Milestone 1`?**
  _High betweenness centrality (0.208) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `AgentContext` (e.g. with `.HandleAuthenticateAsync()` and `.Registration_grants_only_allowlisted_scopes()`) actually correct?**
  _`AgentContext` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Model`, `Id`, `Name` to the rest of the system?**
  _400 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.053189577717879605 - nodes in this community are weakly interconnected._
- **Why does `Knowledge graph` connect `Knowledge graph` to `Brain UI`?**
  _High betweenness centrality (0.198) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.06022408963585434 - nodes in this community are weakly interconnected._
- **Should `JsonElement` be split into smaller, more focused modules?**
  _Cohesion score 0.05540499849442939 - nodes in this community are weakly interconnected._