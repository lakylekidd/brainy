# Graph Report - homelab-brain  (2026-10-18)

## Corpus Check
- 360 files · ~145,764 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 14 file(s) not represented in the graph (top: .css 4, (none) 3, .ttf 3)

## Summary
- 2201 nodes · 7644 edges · 91 communities (68 shown, 23 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 806 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7ed9783a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- HttpClient
- DateTimeOffset
- MetadataDocumentProcessor
- Guid
- KnowledgeDocument
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
- ActivityEvent
- InlineData
- MetadataApiTests
- AgentMemory
- ActivityEventQueue
- .AddBrain
- BrainDbContext
- Fact
- BrainFixture
- system_text_json
- BrainTools
- RegistryApiTests
- Device
- ProvisioningPlan
- EmbeddingModel
- AgentContext
- GitKnowledgeWriter
- McpTests
- AuditEvent
- IReadOnlyList
- KnowledgeProposalApiTests
- ProvisioningView
- Agent
- DeviceApiTests.cs
- IActivityPublisher
- Regex
- ActivityStatus
- RegistryIndexEntry
- compilerOptions
- IKnowledgeProposalStore
- system_text_regularexpressions
- RetainedActivityEvent
- ILogger
- RuntimeView
- .ProcessAsync
- ActivityApiTests.cs
- .Require
- .Agent
- .OnModelCreating
- Agent Instructions
- IAsyncDisposable
- ActivityHarness
- MetadataScanState
- ActivitySanitizer
- McpRegistration.cs
- AgentAuthenticationHandler
- ApprovedPackage
- KnowledgeChunk
- @playwright/test
- CompatibleMetadataProvider
- .ReplaceAsync
- MemoryEmbedding
- SemanticMetadata
- Test-Mcp.ps1
- RecordingExporter
- install-brain-client.sh
- vite-env.d.ts
- ProviderHandler
- KnowledgeOperation
- .Value

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
- `DirectoryStub` --implements--> `IAgentDirectory`  [EXTRACTED]
  tests/Brain.UnitTests/DeviceClientTests.cs → src/Brain.Application/Contracts.cs
- `DirectoryStub` --references--> `AgentContext`  [EXTRACTED]
  tests/Brain.UnitTests/DeviceClientTests.cs → src/Brain.Domain/Identity.cs
- `GraphTests` --references--> `AgentContext`  [EXTRACTED]
  tests/Brain.UnitTests/GraphTests.cs → src/Brain.Domain/Identity.cs

## Import Cycles
- None detected.

## Communities (91 total, 23 thin omitted)

### Community 0 - "Task"
Cohesion: 0.08
Nodes (5): PermissionView, RoleView, HarnessInfo, IHarnessAdapter, Name

### Community 1 - "HttpClient"
Cohesion: 0.05
Nodes (22): AgentLoop, ClientState, ConnectedHarnesses, DirectoryPath, FilePath, MacKeychain, SecureCredentialStore, Service (+14 more)

### Community 2 - "DateTimeOffset"
Cohesion: 0.10
Nodes (10): Brain.Infrastructure.Migrations, InitialFoundation, KnowledgeIndex, SafeKnowledgeWrites, SkillsAndTools, MetadataEnrichment, ActivityTracing, KnowledgeGraph (+2 more)

### Community 3 - "MetadataDocumentProcessor"
Cohesion: 0.28
Nodes (5): VaultDocument, MetadataPatch, MetadataDocumentProcessor, SchemaHash, MetadataTests

### Community 4 - "Guid"
Cohesion: 0.10
Nodes (14): DeviceCredential, DeviceHeartbeatRequest, DeviceRegistration, DeviceView, IDeviceStore, RegisterDeviceRequest, UpdateDeviceRequest, DeviceService (+6 more)

### Community 5 - "KnowledgeDocument"
Cohesion: 0.07
Nodes (31): ChunkedMarkdown, EmbeddedKnowledgeChunk, IKnowledgeIndex, IKnowledgeVault, IMarkdownChunker, Version, KnowledgeDocumentView, KnowledgeResultMetadata (+23 more)

### Community 6 - "GraphSource"
Cohesion: 0.10
Nodes (16): GraphEndpoints, GraphEntity, GraphEntityPage, GraphIds, GraphQuery, GraphReference, GraphRelationship, GraphResult (+8 more)

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
Nodes (4): AgentView, IAgentDirectory, AgentDirectory, ContractDirectory

### Community 11 - "Brain.Domain"
Cohesion: 0.21
Nodes (3): Brain.Infrastructure, Brain.IntegrationTests, Brain.Domain

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
Cohesion: 0.13
Nodes (16): RegistryAccess, CapabilitySearchResult, RegistryIndexStatus, RegistryMetadata, RegistryResult, RegistryScanResult, RegistrySearchRequest, SkillView (+8 more)

### Community 17 - "CommandHarnessAdapter"
Cohesion: 0.12
Nodes (18): ClaudeCodeAdapter, CheckArguments, RemoveArguments, CodexAdapter, CheckArguments, RemoveArguments, CommandHarnessAdapter, CheckArguments (+10 more)

### Community 18 - "client.ts"
Cohesion: 0.10
Nodes (30): apiBaseUrl, asResource(), AuditEvent, DashboardData, isUnauthorized(), loadDashboard(), MetadataIssue, Proposal (+22 more)

### Community 20 - ".Parse"
Cohesion: 0.12
Nodes (4): FileSystemRegistrySource, RegistryOptions, RootPath, RegistrySourceTests

### Community 21 - "DeviceRuntime"
Cohesion: 0.13
Nodes (15): DeviceRuntime, Architecture, CommandsJson, DeviceId, DockerAvailable, EnvironmentId, Healthy, Kind (+7 more)

### Community 22 - "KnowledgeWriteProposal"
Cohesion: 0.06
Nodes (31): KnowledgeWriteProposal, AgentId, AppliedAt, BaseContentHash, Content, CreatedAt, EnrichmentConfidence, EnrichmentModel (+23 more)

### Community 23 - "ActivityEvent"
Cohesion: 0.09
Nodes (17): ActivityEvent, ActivityFilter, ActivityFiltering, ActivityQueryService, ActivityTracingOptions, CleanupIntervalSeconds, OtlpEndpoint, QueueCapacity (+9 more)

### Community 24 - "InlineData"
Cohesion: 0.12
Nodes (5): Program, ApiContractTests, ContractFactory, AdminTokenHash, BrainFactory

### Community 25 - "MetadataApiTests"
Cohesion: 0.16
Nodes (11): KnowledgeProposalPreview, KnowledgeProposalView, Type, KnowledgeWriterResult, MetadataIssueView, FakeProvider, Calls, DuringCall (+3 more)

### Community 26 - "AgentMemory"
Cohesion: 0.06
Nodes (36): AgentMemory, AgentId, Confidence, Content, CreatedAt, ExpiresAt, Id, Importance (+28 more)

### Community 27 - "ActivityEventQueue"
Cohesion: 0.16
Nodes (5): ActivityEventQueue, InstanceId, Reader, BrainActivityPublisher, OutboundActivityObserver

### Community 28 - ".AddBrain"
Cohesion: 0.12
Nodes (17): DocumentProvenance, IKnowledgeProvenance, IMetadataDocumentProcessor, SchemaHash, MetadataInspection, MetadataIssue, MetadataJson, MetadataOptions (+9 more)

### Community 29 - "BrainDbContext"
Cohesion: 0.08
Nodes (20): AuditLog, BrainDbContext, ActivityEvents, ActivityTraces, Agents, AuditEvents, DeviceRuntimes, Devices (+12 more)

### Community 30 - "Fact"
Cohesion: 0.18
Nodes (5): CreateAgentRequest, RememberRequest, UpdateAccessRequest, AgentApiTests, MemoryApiTests

### Community 31 - "BrainFixture"
Cohesion: 0.09
Nodes (7): BrainFixture, ConnectionString, Factory, RegistryPath, VaultPath, DeviceApiTests, UiAdminApiTests

### Community 33 - "BrainTools"
Cohesion: 0.18
Nodes (8): BrainSearchRequest, BrainSearchResult, ICurrentAgent, Context, SearchService, SearchSourceFailure, BrainTools, McpRegistration

### Community 34 - "RegistryApiTests"
Cohesion: 0.24
Nodes (7): IActivityPublisher, IAuditLog, IRegistryIndex, IRegistrySource, MutatingProvider, Model, RegistryApiTests

### Community 35 - "Device"
Cohesion: 0.08
Nodes (24): Device, AgentId, Architecture, ClientVersion, CredentialHash, DisplayName, Hostname, Id (+16 more)

### Community 36 - "ProvisioningPlan"
Cohesion: 0.08
Nodes (24): ProvisioningPlan, ApprovedAt, ApprovedBy, Architecture, CompletedAt, CreatedAt, DeviceId, Id (+16 more)

### Community 37 - "EmbeddingModel"
Cohesion: 0.17
Nodes (13): EmbeddingModel, IEmbeddingProvider, Model, IMemoryStore, MemorySearchRequest, ScoredMemory, MemoryStore, MutatingProvider (+5 more)

### Community 38 - "AgentContext"
Cohesion: 0.15
Nodes (6): DeviceEndpoints, KnowledgeProposalService, ProvisioningService, AgentContext, BrainException, StatusCode

### Community 39 - "GitKnowledgeWriter"
Cohesion: 0.08
Nodes (14): PreparedKnowledgeWrite, FileSystemKnowledgeVault, KnowledgeOptions, MaximumDocumentBytes, VaultPath, WriterEnabled, RevisionTokens, GitKnowledgeWriter (+6 more)

### Community 40 - "McpTests"
Cohesion: 0.22
Nodes (4): MemorySearchResult, MemoryView, MemorySearchResponse, McpTests

### Community 41 - "AuditEvent"
Cohesion: 0.11
Nodes (10): Permissions, ProposalConcurrencyException, AuditEvent, Action, AgentId, Id, MetadataJson, Resource (+2 more)

### Community 42 - "IReadOnlyList"
Cohesion: 0.35
Nodes (6): DeviceSkillReport, DeviceSkillService, DeviceSkillSyncRequest, DeviceSkillView, IDeviceSkillStore, DeviceSkillStore

### Community 43 - "KnowledgeProposalApiTests"
Cohesion: 0.36
Nodes (3): ProposeKnowledgeChangeRequest, ReviewKnowledgeProposalRequest, KnowledgeProposalApiTests

### Community 44 - "ProvisioningView"
Cohesion: 0.23
Nodes (4): IProvisioningStore, ProvisioningView, ProvisionResultRequest, ProvisioningStore

### Community 45 - "Agent"
Cohesion: 0.10
Nodes (21): ProvisioningOptions, Packages, Agent, CreatedAt, Description, Enabled, Id, MetadataJson (+13 more)

### Community 47 - "IActivityPublisher"
Cohesion: 0.17
Nodes (5): ActivityOperation, ActivityPublisherExtensions, IActivityScope, Scope, WorkerActivity

### Community 49 - "ActivityStatus"
Cohesion: 0.11
Nodes (18): ActivityStatus, Cancelled, Failed, Running, Started, Succeeded, ActivityTrace, ActiveRoots (+10 more)

### Community 50 - "RegistryIndexEntry"
Cohesion: 0.11
Nodes (19): RegistryIndexEntry, Capabilities, ContentHash, Description, Embedding, EmbeddingDimensions, EmbeddingModel, EmbeddingProvider (+11 more)

### Community 51 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 52 - "IKnowledgeProposalStore"
Cohesion: 0.19
Nodes (9): IKnowledgeProposalStore, KnowledgeProposalStatus, Applied, Approved, Conflict, Pending, Rejected, Validating (+1 more)

### Community 53 - "system_text_regularexpressions"
Cohesion: 0.10
Nodes (4): Brain.UnitTests, Brain.Application, DependencyView, ProvisionRequest

### Community 54 - "RetainedActivityEvent"
Cohesion: 0.12
Nodes (17): RetainedActivityEvent, Action, AgentId, DurationMs, ErrorMessage, ErrorType, EventType, Id (+9 more)

### Community 55 - "ILogger"
Cohesion: 0.31
Nodes (8): ActivityDispatchService, ActivityRetentionCleanup, DatabaseStartup, ExpiredMemoryCleanup, KnowledgeIndexing, KnowledgeWriteProcessing, MetadataScanning, RegistryIndexing

### Community 56 - "RuntimeView"
Cohesion: 0.24
Nodes (6): DependencyService, IRuntimeStore, RuntimeReportRequest, RuntimeService, RuntimeView, RuntimeStore

### Community 57 - ".ProcessAsync"
Cohesion: 0.36
Nodes (3): ICanonicalKnowledgeWriter, KnowledgeWriterService, ServiceRegistration

### Community 59 - ".Require"
Cohesion: 0.16
Nodes (11): MemoryService, Validation, DevelopmentEmbeddingProvider, Model, InvalidEmbedding, Model, MemoryServiceTests, RecordingAudit (+3 more)

### Community 61 - ".OnModelCreating"
Cohesion: 0.15
Nodes (11): DeviceSkill, DeviceId, Harness, SkillId, SourceHash, Status, UpdatedAt, DeviceSkillStatus (+3 more)

### Community 62 - "Agent Instructions"
Cohesion: 0.13
Nodes (14): Agent Instructions, Architecture Rules, Before making changes, Codebase Knowledge, Coding Guidelines, Core Principles, Documentation, Git (+6 more)

### Community 63 - "IAsyncDisposable"
Cohesion: 0.18
Nodes (5): KnowledgeIndex, ScanLease, KnowledgeProposalStore, WriterLease, Lease

### Community 64 - "ActivityHarness"
Cohesion: 0.33
Nodes (4): ActivityHarness, Publisher, Queue, ActivityTests

### Community 65 - "MetadataScanState"
Cohesion: 0.12
Nodes (15): IMetadataScanStore, MetadataScanState, CheckedAt, ContentHash, IssuesJson, LastEnrichedAt, LastError, Model (+7 more)

### Community 68 - "AgentAuthenticationHandler"
Cohesion: 0.09
Nodes (10): ActivityEndpoints, ActivityParameters, AgentAuthenticationHandler, AgentAuthenticationOptions, AgentTokenHashes, UiAdminAgentId, UiAdminTokenHash, RequestAgent (+2 more)

### Community 69 - "ApprovedPackage"
Cohesion: 0.25
Nodes (8): ApprovedPackage, Architecture, OperatingSystem, PackageId, Requirement, Sha256, Version, WheelUrl

### Community 70 - "KnowledgeChunk"
Cohesion: 0.20
Nodes (10): KnowledgeChunk, Content, Document, DocumentId, Embedding, EndLine, Heading, Id (+2 more)

### Community 72 - "CompatibleMetadataProvider"
Cohesion: 0.21
Nodes (9): IMetadataEnrichmentProvider, Info, MetadataEnrichmentInput, CompatibleMetadataProvider, Info, MetadataProviderOptions, ApiKey, Endpoint (+1 more)

### Community 73 - ".ReplaceAsync"
Cohesion: 0.22
Nodes (5): EmbeddedRegistryDefinition, RegistryHit, Lease, RegistryIndex, WorkerReadinessTests

### Community 74 - "MemoryEmbedding"
Cohesion: 0.29
Nodes (7): MemoryEmbedding, Dimensions, Embedding, Memory, MemoryId, Model, Provider

### Community 75 - "SemanticMetadata"
Cohesion: 0.18
Nodes (11): SemanticMetadata, Confidence, Domains, Environment, Projects, RelatedConcepts, Services, Summary (+3 more)

### Community 89 - "KnowledgeOperation"
Cohesion: 0.33
Nodes (5): KnowledgeOperation, Append, CreateDocument, MetadataEnrichment, ReplaceSection

## Knowledge Gaps
- **400 isolated node(s):** `Action`, `AgentId`, `Id`, `MetadataJson`, `Resource` (+395 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 616 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **23 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BrainDbContext` connect `BrainDbContext` to `Task`, `Guid`, `KnowledgeDocument`, `GraphSource`, `AgentContext`, `Brain.Domain`, `ActivityEvent`, `DeviceRuntime`, `KnowledgeWriteProposal`, `ActivityEvent`, `InlineData`, `MetadataApiTests`, `AgentMemory`, `.AddBrain`, `Fact`, `BrainFixture`, `RegistryApiTests`, `Device`, `ProvisioningPlan`, `EmbeddingModel`, `McpTests`, `AuditEvent`, `IReadOnlyList`, `KnowledgeProposalApiTests`, `ProvisioningView`, `Agent`, `ActivityStatus`, `RegistryIndexEntry`, `RetainedActivityEvent`, `RuntimeView`, `.Agent`, `.OnModelCreating`, `IAsyncDisposable`, `MetadataScanState`, `KnowledgeChunk`, `.ReplaceAsync`, `MemoryEmbedding`?**
  _High betweenness centrality (0.053) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `AgentContext` (e.g. with `.HandleAuthenticateAsync()` and `.Registration_grants_only_allowlisted_scopes()`) actually correct?**
  _`AgentContext` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Action`, `AgentId`, `Id` to the rest of the system?**
  _400 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.08346134152585766 - nodes in this community are weakly interconnected._
- **Why does `Brain.Domain` connect `Brain.Domain` to `Guid`, `KnowledgeDocument`, `GraphSource`, `RegistryService`, `DeviceRuntime`, `ActivityEvent`, `MetadataApiTests`, `AgentMemory`, `.AddBrain`, `system_text_json`, `BrainTools`, `Device`, `ProvisioningPlan`, `EmbeddingModel`, `AuditEvent`, `IReadOnlyList`, `DeviceApiTests.cs`, `ActivityStatus`, `system_text_regularexpressions`, `RuntimeView`, `ActivityApiTests.cs`, `.OnModelCreating`, `MetadataScanState`, `McpRegistration.cs`, `KnowledgeOperation`?**
  _High betweenness centrality (0.026) - this node is a cross-community bridge._
- **Should `HttpClient` be split into smaller, more focused modules?**
  _Cohesion score 0.05490296220633299 - nodes in this community are weakly interconnected._
- **Why does `BrainException` connect `AgentContext` to `MetadataDocumentProcessor`, `Guid`, `KnowledgeDocument`, `GraphSource`, `AgentContext`, `RegistryService`, `.Parse`, `ActivityEvent`, `InlineData`, `.AddBrain`, `Fact`, `BrainTools`, `RegistryApiTests`, `GitKnowledgeWriter`, `AuditEvent`, `IReadOnlyList`, `ProvisioningView`, `IActivityPublisher`, `RuntimeView`, `.ProcessAsync`, `.Require`, `AgentAuthenticationHandler`, `CompatibleMetadataProvider`?**
  _High betweenness centrality (0.025) - this node is a cross-community bridge._