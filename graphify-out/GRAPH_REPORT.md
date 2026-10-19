# Graph Report - homelab-brain  (2026-10-19)

## Corpus Check
- 363 files · ~146,768 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 15 file(s) not represented in the graph (top: (none) 4, .css 4, .ttf 3)

## Summary
- 2288 nodes · 7755 edges · 96 communities (73 shown, 23 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 825 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `a56b77d4`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- HttpClient
- DateTimeOffset
- JsonElement
- Guid
- KnowledgeDocument
- AgentContext
- KnowledgeGraph.tsx
- PavDashboard.tsx
- flow.ts
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
- InlineData
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
- AuditEvent
- .Agent
- GitKnowledgeWriter
- McpTests
- .AddBrainActivity
- IReadOnlyList
- KnowledgeProposalApiTests
- ProvisioningView
- GraphSource
- DeviceApiTests.cs
- IActivityPublisher
- system_text_regularexpressions
- .Plan
- ActivityApiTests.cs
- compilerOptions
- VaultSandbox
- BrainException
- RetainedActivityEvent
- ILogger
- KnowledgeApiTests
- .Require
- ActivityApiTests.cs
- .Require
- .StreamAsync
- .OnModelCreating
- Agent Instructions
- IAsyncDisposable
- ActivityHarness
- MetadataScanState
- MemoryEmbedding
- McpRegistration.cs
- AgentAuthenticationHandler
- Brain UI roadmap
- Brain UI
- RuntimeView
- EmbeddingModel
- KnowledgeOptions
- .AddBrain
- RegistryIndex
- Test-Mcp.ps1
- ActivityEvent
- install-brain-client.sh
- vite-env.d.ts
- DevelopmentEmbeddingProvider
- Local setup
- Development and verification
- API and authentication
- README.md
- MemoryType
- Knowledge graph
- PromotionStatus

## God Nodes (most connected - your core abstractions)
1. `AgentContext` - 126 edges
2. `BrainException` - 108 edges
3. `BrainDbContext` - 103 edges
4. `Brain.Domain` - 84 edges
5. `Brain.Application` - 80 edges
6. `KnowledgeWriteProposal` - 68 edges
7. `AuditEvent` - 61 edges
8. `Brain.Infrastructure` - 61 edges
9. `AgentMemory` - 37 edges
10. `RegistryService` - 35 edges

## Surprising Connections (you probably didn't know these)
- `Data ownership and persistence` --references--> `BrainDbContext`  [INFERRED]
  docs/architecture.md → src/Brain.Infrastructure/BrainDbContext.cs
- `Operational constraints and limits` --references--> `IActivityPublisher`  [INFERRED]
  docs/architecture.md → src/Brain.Application/ActivityContracts.cs
- `Identity read contracts` --references--> `IAgentDirectory`  [INFERRED]
  docs/ui.md → src/Brain.Application/Contracts.cs
- `Identity and request flow` --references--> `IAgentDirectory`  [INFERRED]
  docs/architecture.md → src/Brain.Application/Contracts.cs
- `Retrieval and indexing` --references--> `SearchService`  [INFERRED]
  docs/architecture.md → src/Brain.Application/SearchService.cs

## Import Cycles
- None detected.

## Communities (96 total, 23 thin omitted)

### Community 0 - "Task"
Cohesion: 0.07
Nodes (5): IMetadataScanStore, IHarnessAdapter, Name, Lease, MetadataScanStore

### Community 1 - "HttpClient"
Cohesion: 0.05
Nodes (22): AgentLoop, ClientState, ConnectedHarnesses, DirectoryPath, FilePath, MacKeychain, SecureCredentialStore, Service (+14 more)

### Community 2 - "DateTimeOffset"
Cohesion: 0.06
Nodes (29): Brain.Infrastructure.Migrations, InitialFoundation, KnowledgeIndex, SafeKnowledgeWrites, SkillsAndTools, MetadataEnrichment, ActivityTracing, KnowledgeGraph (+21 more)

### Community 3 - "JsonElement"
Cohesion: 0.06
Nodes (39): VaultDocument, DocumentProvenance, IMetadataDocumentProcessor, SchemaHash, MetadataEnrichmentInput, MetadataInspection, MetadataIssue, MetadataJson (+31 more)

### Community 4 - "Guid"
Cohesion: 0.11
Nodes (12): DeviceCredential, DeviceHeartbeatRequest, DeviceRegistration, IDeviceStore, RegisterDeviceRequest, UpdateDeviceRequest, DeviceStore, DeviceClientTests (+4 more)

### Community 5 - "KnowledgeDocument"
Cohesion: 0.07
Nodes (36): ChunkedMarkdown, EmbeddedKnowledgeChunk, IMarkdownChunker, Version, KnowledgeDocumentView, KnowledgeResultMetadata, KnowledgeScanFailure, KnowledgeScanResult (+28 more)

### Community 6 - "AgentContext"
Cohesion: 0.17
Nodes (9): Retrieval and indexing, GraphQuery, IGraphSource, GraphService, AgentContext, Permissions, GraphTests, LargeSource (+1 more)

### Community 7 - "KnowledgeGraph.tsx"
Cohesion: 0.08
Nodes (32): sigma, ApiError, request(), canvasFont, canvasPalette(), emptyGraph, EntityType, entityTypes (+24 more)

### Community 8 - "PavDashboard.tsx"
Cohesion: 0.05
Nodes (75): d3, demoGraph(), areaSectors(), createLayout(), Guide, Layout, nodePlacement(), Placement (+67 more)

### Community 9 - "flow.ts"
Cohesion: 0.42
Nodes (7): drawSignal(), flowProgress(), linkControl(), linkPoint(), Point, signalDuration(), signalPath()

### Community 10 - "AgentContext"
Cohesion: 0.12
Nodes (7): DeviceEndpoints, AgentView, CreateAgentRequest, IAgentDirectory, AgentDirectory, AgentApiTests, ContractDirectory

### Community 11 - "Brain.Domain"
Cohesion: 0.16
Nodes (4): Brain.Infrastructure, Brain.UnitTests, Brain.Domain, Brain.Application

### Community 12 - "Brain.Infrastructure"
Cohesion: 0.07
Nodes (39): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.AspNetCore.OpenApi (10.0.13), Microsoft.EntityFrameworkCore (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Microsoft.Extensions.Configuration.Binder (10.0.13) (+31 more)

### Community 13 - "package.json"
Cohesion: 0.06
Nodes (29): graphology, jsdom, @testing-library/jest-dom, @testing-library/react, @types/d3, @types/react, @types/react-dom, typescript (+21 more)

### Community 14 - "Agents.tsx"
Cohesion: 0.09
Nodes (24): @playwright/test, AgentView, Credentials, AccessForm(), submit(), SearchMultiSelect(), Agents(), Directory (+16 more)

### Community 15 - "App.tsx"
Cohesion: 0.11
Nodes (30): lucide-react, react, react-dom, AgentProfile, authenticate(), Agents, App(), navigate() (+22 more)

### Community 16 - "RegistryService"
Cohesion: 0.12
Nodes (17): IActivityPublisher, IAuditLog, RegistryAccess, CapabilitySearchResult, IRegistryIndex, IRegistrySource, RegistryMetadata, RegistryResult (+9 more)

### Community 17 - "CommandHarnessAdapter"
Cohesion: 0.10
Nodes (19): ClaudeCodeAdapter, CheckArguments, RemoveArguments, CodexAdapter, CheckArguments, RemoveArguments, CommandHarnessAdapter, CheckArguments (+11 more)

### Community 18 - "client.ts"
Cohesion: 0.10
Nodes (30): apiBaseUrl, asResource(), AuditEvent, DashboardData, isUnauthorized(), loadDashboard(), MetadataIssue, Proposal (+22 more)

### Community 19 - "ActivityEvent"
Cohesion: 0.16
Nodes (3): ActivityEvent, IActivityStore, ActivityApiTests

### Community 20 - "InlineData"
Cohesion: 0.05
Nodes (12): Program, GraphIds, IGraphReferenceExtractor, FileSystemRegistrySource, RegistryOptions, RootPath, GraphReferenceExtractor, ApiContractTests (+4 more)

### Community 21 - "DeviceRuntime"
Cohesion: 0.12
Nodes (15): DeviceRuntime, Architecture, CommandsJson, DeviceId, DockerAvailable, EnvironmentId, Healthy, Kind (+7 more)

### Community 22 - "KnowledgeWriteProposal"
Cohesion: 0.06
Nodes (32): ProposalConcurrencyException, KnowledgeWriteProposal, AgentId, AppliedAt, BaseContentHash, Content, CreatedAt, EnrichmentConfidence (+24 more)

### Community 23 - "ActivityFilter"
Cohesion: 0.25
Nodes (5): ActivityFilter, ActivityFiltering, ActivityQueryService, TraceView, ActivityStore

### Community 24 - "Regex"
Cohesion: 0.19
Nodes (4): HeadingSpan, KnowledgeChangePlanner, MarkdownChunker, Version

### Community 25 - "MetadataApiTests"
Cohesion: 0.17
Nodes (9): KnowledgeProposalPreview, MetadataIssueView, MetadataScanResult, FakeProvider, Calls, DuringCall, Info, Model (+1 more)

### Community 26 - "AgentMemory"
Cohesion: 0.11
Nodes (18): AgentMemory, AgentId, Confidence, Content, CreatedAt, ExpiresAt, Id, Importance (+10 more)

### Community 27 - "ActivitySanitizer"
Cohesion: 0.20
Nodes (3): BrainActivityPublisher, ActivitySanitizer, SanitizingActivityProcessor

### Community 28 - "devDependencies"
Cohesion: 0.17
Nodes (12): devDependencies, jsdom, @playwright/test, @testing-library/jest-dom, @testing-library/react, @types/d3, @types/react, @types/react-dom (+4 more)

### Community 29 - "BrainDbContext"
Cohesion: 0.08
Nodes (20): BrainDbContext, ActivityEvents, ActivityTraces, Agents, AuditEvents, DeviceRuntimes, Devices, DeviceSkills (+12 more)

### Community 31 - "BrainFixture"
Cohesion: 0.09
Nodes (7): BrainFixture, ConnectionString, Factory, RegistryPath, VaultPath, DeviceApiTests, UiAdminApiTests

### Community 33 - "BrainTools"
Cohesion: 0.20
Nodes (7): Identity and request flow, BrainSearchResult, ICurrentAgent, Context, SearchSourceFailure, BrainTools, SkillSearchResponse

### Community 34 - "RegistryApiTests"
Cohesion: 0.29
Nodes (4): UpdateAccessRequest, RegistryScanResult, SkillView, RegistryApiTests

### Community 35 - "Device"
Cohesion: 0.08
Nodes (24): Device, AgentId, Architecture, ClientVersion, CredentialHash, DisplayName, Hostname, Id (+16 more)

### Community 36 - "ProvisioningPlan"
Cohesion: 0.08
Nodes (24): ProvisioningPlan, ApprovedAt, ApprovedBy, Architecture, CompletedAt, CreatedAt, DeviceId, Id (+16 more)

### Community 37 - "AuditEvent"
Cohesion: 0.17
Nodes (10): EmbeddedRegistryDefinition, AuditEvent, Action, AgentId, Id, MetadataJson, Resource, Result (+2 more)

### Community 39 - "GitKnowledgeWriter"
Cohesion: 0.23
Nodes (4): IKnowledgeVault, FileSystemKnowledgeVault, RevisionTokens, KnowledgeTests

### Community 40 - "McpTests"
Cohesion: 0.18
Nodes (4): MemorySearchResult, MemoryView, MemorySearchResponse, McpTests

### Community 41 - ".AddBrainActivity"
Cohesion: 0.11
Nodes (13): ActivityTracingOptions, CleanupIntervalSeconds, OtlpEndpoint, QueueCapacity, RetentionDays, SubscriberCapacity, IActivityStream, ActivityLiveBus (+5 more)

### Community 42 - "IReadOnlyList"
Cohesion: 0.14
Nodes (8): PermissionView, RoleView, DeviceSkillReport, DeviceSkillService, DeviceSkillSyncRequest, DeviceSkillView, IDeviceSkillStore, DeviceSkillStore

### Community 43 - "KnowledgeProposalApiTests"
Cohesion: 0.19
Nodes (11): ICanonicalKnowledgeWriter, IKnowledgeProposalStore, KnowledgeProposalView, Type, KnowledgeWriterResult, PreparedKnowledgeWrite, ProposeKnowledgeChangeRequest, ReviewKnowledgeProposalRequest (+3 more)

### Community 44 - "ProvisioningView"
Cohesion: 0.12
Nodes (9): DeviceView, DeviceService, IProvisioningStore, ProvisioningService, ProvisioningView, ProvisionResultRequest, BrainException, StatusCode (+1 more)

### Community 45 - "GraphSource"
Cohesion: 0.06
Nodes (36): GraphEntity, GraphEntityPage, GraphReference, GraphRelationship, GraphResult, ApprovedPackage, Architecture, OperatingSystem (+28 more)

### Community 47 - "IActivityPublisher"
Cohesion: 0.18
Nodes (5): ActivityOperation, ActivityPublisherExtensions, IActivityScope, Scope, WorkerActivity

### Community 49 - ".Plan"
Cohesion: 0.12
Nodes (12): KnowledgeOperation, Append, CreateDocument, MetadataEnrichment, ReplaceSection, KnowledgeProposalStatus, Applied, Approved (+4 more)

### Community 51 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 52 - "VaultSandbox"
Cohesion: 0.28
Nodes (4): KnowledgeWriteTests, TestGit, VaultSandbox, Root

### Community 54 - "RetainedActivityEvent"
Cohesion: 0.06
Nodes (35): ActivityStatus, Cancelled, Failed, Running, Started, Succeeded, ActivityTrace, ActiveRoots (+27 more)

### Community 55 - "ILogger"
Cohesion: 0.33
Nodes (8): ActivityDispatchService, ActivityRetentionCleanup, DatabaseStartup, ExpiredMemoryCleanup, KnowledgeIndexing, KnowledgeWriteProcessing, MetadataScanning, RegistryIndexing

### Community 57 - ".Require"
Cohesion: 0.18
Nodes (3): KnowledgeProposalService, BrainSearchRequest, Validation

### Community 59 - ".Require"
Cohesion: 0.25
Nodes (8): MemoryService, FailingEmbedding, Model, MemoryServiceTests, RecordingAudit, RecordingStore, Audit, Vector

### Community 60 - ".StreamAsync"
Cohesion: 0.18
Nodes (6): ActivityEndpoints, ActivityParameters, RequestAgent, Context, DeviceId, GraphEndpoints

### Community 61 - ".OnModelCreating"
Cohesion: 0.15
Nodes (11): DeviceSkill, DeviceId, Harness, SkillId, SourceHash, Status, UpdatedAt, DeviceSkillStatus (+3 more)

### Community 62 - "Agent Instructions"
Cohesion: 0.13
Nodes (14): Agent Instructions, Architecture Rules, Before making changes, Codebase Knowledge, Coding Guidelines, Core Principles, Documentation, Git (+6 more)

### Community 63 - "IAsyncDisposable"
Cohesion: 0.19
Nodes (4): KnowledgeIndex, ScanLease, KnowledgeProposalStore, WriterLease

### Community 64 - "ActivityHarness"
Cohesion: 0.33
Nodes (4): ActivityHarness, Publisher, Queue, ActivityTests

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
Cohesion: 0.17
Nodes (12): Agents and permissions, Appearance, Authentication and safety, Brain UI, Current scope: UI Milestones 1-4, End-to-end verification, Existing API coverage and gaps, Identity read contracts (+4 more)

### Community 72 - "EmbeddingModel"
Cohesion: 0.15
Nodes (15): EmbeddingModel, IEmbeddingProvider, Model, IMemoryStore, MemorySearchRequest, ScoredMemory, MemoryStore, MutatingProvider (+7 more)

### Community 73 - "KnowledgeOptions"
Cohesion: 0.50
Nodes (4): KnowledgeOptions, MaximumDocumentBytes, VaultPath, WriterEnabled

### Community 74 - ".AddBrain"
Cohesion: 0.11
Nodes (18): Architecture, Code organization, Data ownership and persistence, Metadata enrichment, Operational constraints and limits, Purpose and boundaries, Reviewed canonical changes, Runtime topology (+10 more)

### Community 75 - "RegistryIndex"
Cohesion: 0.31
Nodes (4): RegistryHit, RegistryIndexStatus, Lease, RegistryIndex

### Community 88 - "DevelopmentEmbeddingProvider"
Cohesion: 0.39
Nodes (3): DevelopmentEmbeddingProvider, Model, WorkerReadinessTests

### Community 89 - "Local setup"
Cohesion: 0.29
Nodes (7): Frontend hot reload, Local setup, Production boundary, Requirements, Services and ports, Start the stack, Vault and development data

### Community 90 - "Development and verification"
Cohesion: 0.33
Nodes (5): Brain Client, Build and test, Development and verification, Host-based development, Operational limits

### Community 91 - "API and authentication"
Cohesion: 0.50
Nodes (4): API and authentication, Authentication, Endpoint catalog, Example request

### Community 92 - "README.md"
Cohesion: 0.22
Nodes (4): Documentation, Highlights, Project status, Quick start

### Community 93 - "MemoryType"
Cohesion: 0.25
Nodes (8): MemoryType, Decision, Discovery, Fact, Incident, Observation, Preference, TaskContext

### Community 94 - "Knowledge graph"
Cohesion: 0.50
Nodes (4): Graph contracts and authorization, Knowledge graph, Sources, relationships and synchronization, label()

### Community 98 - "PromotionStatus"
Cohesion: 0.18
Nodes (10): MemoryVisibility, Organization, Private, Team, PromotionStatus, Approved, Candidate, None (+2 more)

## Knowledge Gaps
- **400 isolated node(s):** `Name`, `ConnectedHarnesses`, `DirectoryPath`, `FilePath`, `Service` (+395 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 661 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **23 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Brain UI` connect `Brain UI` to `README.md`, `Knowledge graph`?**
  _High betweenness centrality (0.236) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `AgentContext` (e.g. with `.HandleAuthenticateAsync()` and `.Registration_grants_only_allowlisted_scopes()`) actually correct?**
  _`AgentContext` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Name`, `ConnectedHarnesses`, `DirectoryPath` to the rest of the system?**
  _400 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.06882882882882883 - nodes in this community are weakly interconnected._
- **Why does `VaultCanvas()` connect `PavDashboard.tsx` to `flow.ts`, `Knowledge graph`, `KnowledgeGraph.tsx`?**
  _High betweenness centrality (0.231) - this node is a cross-community bridge._
- **Should `HttpClient` be split into smaller, more focused modules?**
  _Cohesion score 0.05490296220633299 - nodes in this community are weakly interconnected._
- **Why does `Knowledge graph` connect `Knowledge graph` to `Brain UI`?**
  _High betweenness centrality (0.229) - this node is a cross-community bridge._