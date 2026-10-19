# Graph Report - homelab-brain  (2026-10-18)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 2186 nodes · 7630 edges · 88 communities (64 shown, 24 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 806 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d61aaed1`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- HttpClient
- DateTimeOffset
- MetadataDocumentProcessor
- Guid
- .AddBrain
- GraphSource
- KnowledgeGraph.tsx
- PavDashboard.tsx
- VaultCanvas.tsx
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
- InlineData
- MetadataApiTests
- AgentMemory
- ActivityEventQueue
- ActivityApiTests.cs
- BrainDbContext
- Fact
- BrainFixture
- system_text_json
- BrainTools
- RegistryApiTests
- Device
- ProvisioningPlan
- EmbeddingModel
- .Require
- VaultSandbox
- McpTests
- BrainException
- IReadOnlyList
- KnowledgeProposalApiTests
- ProvisioningView
- Agent
- DeviceApiTests.cs
- IActivityPublisher
- Regex
- ActivityStatus
- .RunAsync
- compilerOptions
- .Plan
- system_text_regularexpressions
- RetainedActivityEvent
- ILogger
- KnowledgeApiTests
- .AddBrainActivity
- MemoryServiceTests
- .Agent
- .OnModelCreating
- KnowledgeDocument
- Subscription
- ActivityHarness
- MetadataScanState
- ActivitySanitizer
- McpRegistration.cs
- AgentAuthenticationHandler
- ApprovedPackage
- KnowledgeChunk
- @playwright/test
- MemoryType
- DevelopmentEmbeddingProvider
- MemoryEmbedding
- KnowledgeOptions
- Test-Mcp.ps1
- install-brain-client.sh
- vite-env.d.ts

## God Nodes (most connected - your core abstractions)
1. `AgentContext` - 126 edges
2. `BrainException` - 108 edges
3. `BrainDbContext` - 102 edges
4. `Brain.Domain` - 84 edges
5. `Brain.Application` - 80 edges
6. `KnowledgeWriteProposal` - 68 edges
7. `AuditEvent` - 61 edges
8. `Brain.Infrastructure` - 61 edges
9. `AgentMemory` - 37 edges
10. `RegistryService` - 35 edges

## Surprising Connections (you probably didn't know these)
- `RecordingStore` --implements--> `IMemoryStore`  [EXTRACTED]
  tests/Brain.UnitTests/MemoryServiceTests.cs → src/Brain.Application/Contracts.cs
- `RecordingStore` --references--> `AuditEvent`  [EXTRACTED]
  tests/Brain.UnitTests/MemoryServiceTests.cs → src/Brain.Domain/Memory.cs
- `GraphTests` --references--> `AgentContext`  [EXTRACTED]
  tests/Brain.UnitTests/GraphTests.cs → src/Brain.Domain/Identity.cs
- `MemoryServiceTests` --references--> `AgentContext`  [EXTRACTED]
  tests/Brain.UnitTests/MemoryServiceTests.cs → src/Brain.Domain/Identity.cs
- `FakeProvider` --implements--> `IMetadataEnrichmentProvider`  [EXTRACTED]
  tests/Brain.IntegrationTests/MetadataApiTests.cs → src/Brain.Application/MetadataContracts.cs

## Import Cycles
- None detected.

## Communities (88 total, 24 thin omitted)

### Community 0 - "Task"
Cohesion: 0.05
Nodes (20): IMemoryStore, DeviceCredential, IKnowledgeProposalStore, KnowledgeWriterService, IMetadataScanStore, AuditEvent, Action, AgentId (+12 more)

### Community 1 - "HttpClient"
Cohesion: 0.05
Nodes (22): AgentLoop, ClientState, ConnectedHarnesses, DirectoryPath, FilePath, MacKeychain, SecureCredentialStore, Service (+14 more)

### Community 2 - "DateTimeOffset"
Cohesion: 0.06
Nodes (29): Brain.Infrastructure.Migrations, InitialFoundation, KnowledgeIndex, SafeKnowledgeWrites, SkillsAndTools, MetadataEnrichment, ActivityTracing, KnowledgeGraph (+21 more)

### Community 3 - "MetadataDocumentProcessor"
Cohesion: 0.06
Nodes (41): VaultDocument, DocumentProvenance, IMetadataDocumentProcessor, SchemaHash, IMetadataEnrichmentProvider, Info, MetadataEnrichmentInput, MetadataInspection (+33 more)

### Community 4 - "Guid"
Cohesion: 0.09
Nodes (13): DeviceHeartbeatRequest, DeviceRegistration, DeviceView, IDeviceStore, RegisterDeviceRequest, UpdateDeviceRequest, DeviceService, ProvisioningService (+5 more)

### Community 5 - ".AddBrain"
Cohesion: 0.08
Nodes (21): IAuditLog, ChunkedMarkdown, EmbeddedKnowledgeChunk, IKnowledgeIndex, IKnowledgeVault, IMarkdownChunker, Version, KnowledgeDocumentView (+13 more)

### Community 6 - "GraphSource"
Cohesion: 0.10
Nodes (15): GraphEndpoints, GraphEntity, GraphEntityPage, GraphIds, GraphQuery, GraphRelationship, GraphResult, IGraphReferenceExtractor (+7 more)

### Community 7 - "KnowledgeGraph.tsx"
Cohesion: 0.08
Nodes (29): graphology, sigma, emptyGraph, EntityType, entityTypes, GraphEntity, GraphRelationship, GraphResult (+21 more)

### Community 8 - "PavDashboard.tsx"
Cohesion: 0.09
Nodes (31): demoGraph(), areaSectors(), createLayout(), Guide, Placement, RingId, RINGS, ViewMode (+23 more)

### Community 9 - "VaultCanvas.tsx"
Cohesion: 0.10
Nodes (36): d3, canvasFont, canvasPalette(), drawSignal(), flowProgress(), linkControl(), linkPoint(), Point (+28 more)

### Community 10 - "AgentContext"
Cohesion: 0.14
Nodes (9): DeviceEndpoints, AgentView, IAgentDirectory, PermissionView, RoleView, AgentContext, AgentDirectory, ContractDirectory (+1 more)

### Community 11 - "Brain.Domain"
Cohesion: 0.14
Nodes (6): Brain.Infrastructure, Brain.UnitTests, Brain.Domain, Brain.Tests, Brain.Application, WorkerDatabaseReadiness

### Community 12 - "Brain.Infrastructure"
Cohesion: 0.07
Nodes (39): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.AspNetCore.OpenApi (10.0.13), Microsoft.EntityFrameworkCore (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Microsoft.Extensions.Configuration.Binder (10.0.13) (+31 more)

### Community 13 - "package.json"
Cohesion: 0.05
Nodes (37): jsdom, @testing-library/jest-dom, @types/d3, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react (+29 more)

### Community 14 - "Agents.tsx"
Cohesion: 0.10
Nodes (27): AgentView, ApiError, authenticate(), Credentials, request(), credentials, signIn(), AccessForm() (+19 more)

### Community 15 - "App.tsx"
Cohesion: 0.11
Nodes (23): lucide-react, react, react-dom, @testing-library/react, AgentProfile, Agents, App(), KnowledgeGraph (+15 more)

### Community 16 - "RegistryService"
Cohesion: 0.15
Nodes (11): RegistryAccess, CapabilitySearchResult, RegistryResult, RegistrySearchRequest, RegistryService, RegistryDefinition, Fingerprint, Key (+3 more)

### Community 17 - "CommandHarnessAdapter"
Cohesion: 0.10
Nodes (21): ClaudeCodeAdapter, CheckArguments, RemoveArguments, CodexAdapter, CheckArguments, RemoveArguments, CommandHarnessAdapter, CheckArguments (+13 more)

### Community 18 - "client.ts"
Cohesion: 0.10
Nodes (30): apiBaseUrl, asResource(), AuditEvent, DashboardData, isUnauthorized(), loadDashboard(), MetadataIssue, Proposal (+22 more)

### Community 19 - "ActivityEvent"
Cohesion: 0.16
Nodes (4): ActivityEvent, IActivityStore, TraceView, ActivityApiTests

### Community 20 - ".Parse"
Cohesion: 0.12
Nodes (4): FileSystemRegistrySource, RegistryOptions, RootPath, RegistrySourceTests

### Community 21 - "DeviceRuntime"
Cohesion: 0.10
Nodes (21): DependencyService, IRuntimeStore, RuntimeReportRequest, RuntimeService, RuntimeView, DeviceRuntime, Architecture, CommandsJson (+13 more)

### Community 22 - "KnowledgeWriteProposal"
Cohesion: 0.06
Nodes (31): KnowledgeWriteProposal, AgentId, AppliedAt, BaseContentHash, Content, CreatedAt, EnrichmentConfidence, EnrichmentModel (+23 more)

### Community 23 - "ActivityFilter"
Cohesion: 0.14
Nodes (9): ActivityEndpoints, ActivityParameters, RequestAgent, Context, DeviceId, ActivityFilter, ActivityFiltering, ActivityQueryService (+1 more)

### Community 24 - "InlineData"
Cohesion: 0.12
Nodes (5): Program, ApiContractTests, ContractFactory, AdminTokenHash, BrainFactory

### Community 25 - "MetadataApiTests"
Cohesion: 0.16
Nodes (11): KnowledgeProposalPreview, KnowledgeProposalView, Type, KnowledgeWriterResult, MetadataIssueView, FakeProvider, Calls, DuringCall (+3 more)

### Community 26 - "AgentMemory"
Cohesion: 0.07
Nodes (28): AgentMemory, AgentId, Confidence, Content, CreatedAt, ExpiresAt, Id, Importance (+20 more)

### Community 27 - "ActivityEventQueue"
Cohesion: 0.11
Nodes (7): ActivityEventQueue, InstanceId, Reader, BrainActivityPublisher, OutboundActivityObserver, RecordingExporter, Spans

### Community 29 - "BrainDbContext"
Cohesion: 0.08
Nodes (20): BrainDbContext, ActivityEvents, ActivityTraces, Agents, AuditEvents, DeviceRuntimes, Devices, DeviceSkills (+12 more)

### Community 30 - "Fact"
Cohesion: 0.17
Nodes (5): CreateAgentRequest, RememberRequest, UpdateAccessRequest, AgentApiTests, MemoryApiTests

### Community 31 - "BrainFixture"
Cohesion: 0.09
Nodes (7): BrainFixture, ConnectionString, Factory, RegistryPath, VaultPath, DeviceApiTests, UiAdminApiTests

### Community 33 - "BrainTools"
Cohesion: 0.18
Nodes (8): BrainSearchRequest, BrainSearchResult, ICurrentAgent, Context, SearchService, SearchSourceFailure, BrainTools, McpRegistration

### Community 34 - "RegistryApiTests"
Cohesion: 0.20
Nodes (9): EmbeddedRegistryDefinition, IRegistryIndex, IRegistrySource, RegistryMetadata, RegistryScanResult, SkillView, MutatingProvider, Model (+1 more)

### Community 35 - "Device"
Cohesion: 0.08
Nodes (24): Device, AgentId, Architecture, ClientVersion, CredentialHash, DisplayName, Hostname, Id (+16 more)

### Community 36 - "ProvisioningPlan"
Cohesion: 0.08
Nodes (24): ProvisioningPlan, ApprovedAt, ApprovedBy, Architecture, CompletedAt, CreatedAt, DeviceId, Id (+16 more)

### Community 37 - "EmbeddingModel"
Cohesion: 0.14
Nodes (13): EmbeddingModel, IEmbeddingProvider, Model, MemorySearchRequest, ScoredMemory, RegistryHit, RegistryIndexStatus, Lease (+5 more)

### Community 38 - ".Require"
Cohesion: 0.21
Nodes (3): KnowledgeProposalService, MemoryService, Validation

### Community 39 - "VaultSandbox"
Cohesion: 0.22
Nodes (5): FileSystemKnowledgeVault, RevisionTokens, KnowledgeTests, VaultSandbox, Root

### Community 40 - "McpTests"
Cohesion: 0.22
Nodes (4): MemorySearchResult, MemoryView, MemorySearchResponse, McpTests

### Community 41 - "BrainException"
Cohesion: 0.30
Nodes (5): PreparedKnowledgeWrite, BrainException, StatusCode, GitKnowledgeWriter, GitResult

### Community 42 - "IReadOnlyList"
Cohesion: 0.21
Nodes (6): DeviceSkillReport, DeviceSkillService, DeviceSkillSyncRequest, DeviceSkillView, IDeviceSkillStore, DeviceSkillStore

### Community 43 - "KnowledgeProposalApiTests"
Cohesion: 0.34
Nodes (4): ICanonicalKnowledgeWriter, ProposeKnowledgeChangeRequest, ReviewKnowledgeProposalRequest, KnowledgeProposalApiTests

### Community 44 - "ProvisioningView"
Cohesion: 0.23
Nodes (4): IProvisioningStore, ProvisioningView, ProvisionResultRequest, ProvisioningStore

### Community 45 - "Agent"
Cohesion: 0.11
Nodes (20): Agent, CreatedAt, Description, Enabled, Id, MetadataJson, Name, Permissions (+12 more)

### Community 47 - "IActivityPublisher"
Cohesion: 0.19
Nodes (6): ActivityOperation, ActivityPublisherExtensions, IActivityPublisher, IActivityScope, Scope, WorkerActivity

### Community 48 - "Regex"
Cohesion: 0.20
Nodes (4): HeadingSpan, KnowledgeChangePlanner, MarkdownChunker, Version

### Community 49 - "ActivityStatus"
Cohesion: 0.11
Nodes (18): ActivityStatus, Cancelled, Failed, Running, Started, Succeeded, ActivityTrace, ActiveRoots (+10 more)

### Community 51 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 52 - ".Plan"
Cohesion: 0.12
Nodes (12): KnowledgeOperation, Append, CreateDocument, MetadataEnrichment, ReplaceSection, KnowledgeProposalStatus, Applied, Approved (+4 more)

### Community 54 - "RetainedActivityEvent"
Cohesion: 0.12
Nodes (17): RetainedActivityEvent, Action, AgentId, DurationMs, ErrorMessage, ErrorType, EventType, Id (+9 more)

### Community 55 - "ILogger"
Cohesion: 0.33
Nodes (8): ActivityDispatchService, ActivityRetentionCleanup, DatabaseStartup, ExpiredMemoryCleanup, KnowledgeIndexing, KnowledgeWriteProcessing, MetadataScanning, RegistryIndexing

### Community 57 - ".AddBrainActivity"
Cohesion: 0.15
Nodes (10): ActivityTracingOptions, CleanupIntervalSeconds, OtlpEndpoint, QueueCapacity, RetentionDays, SubscriberCapacity, IActivityStream, ActivityLiveBus (+2 more)

### Community 59 - "MemoryServiceTests"
Cohesion: 0.23
Nodes (9): FailingEmbedding, Model, InvalidEmbedding, Model, MemoryServiceTests, RecordingAudit, RecordingStore, Audit (+1 more)

### Community 61 - ".OnModelCreating"
Cohesion: 0.15
Nodes (11): DeviceSkill, DeviceId, Harness, SkillId, SourceHash, Status, UpdatedAt, DeviceSkillStatus (+3 more)

### Community 62 - "KnowledgeDocument"
Cohesion: 0.13
Nodes (14): KnowledgeDocument, ChunkerVersion, ContentHash, EmbeddingDimensions, EmbeddingModel, EmbeddingProvider, GraphReferences, GraphVersion (+6 more)

### Community 64 - "ActivityHarness"
Cohesion: 0.33
Nodes (4): ActivityHarness, Publisher, Queue, ActivityTests

### Community 65 - "MetadataScanState"
Cohesion: 0.14
Nodes (13): MetadataScanState, CheckedAt, ContentHash, IssuesJson, LastEnrichedAt, LastError, Model, Path (+5 more)

### Community 68 - "AgentAuthenticationHandler"
Cohesion: 0.17
Nodes (5): AgentAuthenticationHandler, AgentAuthenticationOptions, AgentTokenHashes, UiAdminAgentId, UiAdminTokenHash

### Community 69 - "ApprovedPackage"
Cohesion: 0.18
Nodes (10): ApprovedPackage, Architecture, OperatingSystem, PackageId, Requirement, Sha256, Version, WheelUrl (+2 more)

### Community 70 - "KnowledgeChunk"
Cohesion: 0.20
Nodes (10): KnowledgeChunk, Content, Document, DocumentId, Embedding, EndLine, Heading, Id (+2 more)

### Community 72 - "MemoryType"
Cohesion: 0.25
Nodes (8): MemoryType, Decision, Discovery, Fact, Incident, Observation, Preference, TaskContext

### Community 73 - "DevelopmentEmbeddingProvider"
Cohesion: 0.39
Nodes (3): DevelopmentEmbeddingProvider, Model, WorkerReadinessTests

### Community 74 - "MemoryEmbedding"
Cohesion: 0.29
Nodes (7): MemoryEmbedding, Dimensions, Embedding, Memory, MemoryId, Model, Provider

### Community 75 - "KnowledgeOptions"
Cohesion: 0.50
Nodes (4): KnowledgeOptions, MaximumDocumentBytes, VaultPath, WriterEnabled

## Knowledge Gaps
- **400 isolated node(s):** `Action`, `AgentId`, `Id`, `MetadataJson`, `Resource` (+395 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 602 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BrainDbContext` connect `BrainDbContext` to `Task`, `DateTimeOffset`, `Guid`, `.AddBrain`, `GraphSource`, `AgentContext`, `Brain.Domain`, `ActivityEvent`, `DeviceRuntime`, `KnowledgeWriteProposal`, `ActivityFilter`, `InlineData`, `MetadataApiTests`, `AgentMemory`, `Fact`, `BrainFixture`, `RegistryApiTests`, `Device`, `ProvisioningPlan`, `EmbeddingModel`, `McpTests`, `IReadOnlyList`, `KnowledgeProposalApiTests`, `ProvisioningView`, `Agent`, `ActivityStatus`, `RetainedActivityEvent`, `KnowledgeApiTests`, `.Agent`, `.OnModelCreating`, `KnowledgeDocument`, `MetadataScanState`, `KnowledgeChunk`, `DevelopmentEmbeddingProvider`, `MemoryEmbedding`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `AgentContext` (e.g. with `.HandleAuthenticateAsync()` and `.Registration_grants_only_allowlisted_scopes()`) actually correct?**
  _`AgentContext` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Action`, `AgentId`, `Id` to the rest of the system?**
  _400 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.053584764749813295 - nodes in this community are weakly interconnected._
- **Why does `KnowledgeWriteProposal` connect `KnowledgeWriteProposal` to `Task`, `DateTimeOffset`, `Guid`, `.AddBrain`, `.Require`, `BrainException`, `IReadOnlyList`, `.RunAsync`, `.OnModelCreating`, `.Plan`, `MetadataApiTests`, `BrainDbContext`?**
  _High betweenness centrality (0.035) - this node is a cross-community bridge._
- **Should `HttpClient` be split into smaller, more focused modules?**
  _Cohesion score 0.05490296220633299 - nodes in this community are weakly interconnected._
- **Why does `BrainException` connect `BrainException` to `Task`, `MetadataDocumentProcessor`, `Guid`, `.AddBrain`, `GraphSource`, `AgentContext`, `RegistryService`, `.Parse`, `DeviceRuntime`, `ActivityFilter`, `InlineData`, `MetadataApiTests`, `BrainTools`, `RegistryApiTests`, `.Require`, `VaultSandbox`, `IReadOnlyList`, `ProvisioningView`, `Agent`, `IActivityPublisher`, `.RunAsync`, `.Plan`, `KnowledgeApiTests`, `MemoryServiceTests`?**
  _High betweenness centrality (0.034) - this node is a cross-community bridge._