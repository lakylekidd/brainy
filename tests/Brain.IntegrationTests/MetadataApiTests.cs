using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Brain.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class MetadataApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false) } };
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Scan_preview_review_and_writer_apply_metadata_without_altering_human_fields_or_body()
    {
        const string before = "---\n# Human comment\nid: human-id\ntitle: 'Human title'\ntags: [HumanTag]\ncustom:\n  owner: Willem\n---\n# Body\nKeep exact body.\n";
        Write("human.md", before);
        await TestGit.InitializeAsync(fixture.VaultPath);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var scan = await Scan(admin);
        Assert.Equal(1, scan.Proposed); Assert.Equal(0, scan.Failed);
        Assert.Equal(before, File.ReadAllText(Path.Combine(fixture.VaultPath, "human.md")));
        var proposal = Assert.Single((await admin.GetFromJsonAsync<KnowledgeProposalView[]>("/api/knowledge/proposals?type=metadata_enrichment&status=pending&limit=1", Json))!);
        Assert.Equal("metadata_enrichment", proposal.Type); Assert.Equal(KnowledgeProposalStatus.Pending, proposal.Status);
        Assert.Equal("deterministic", proposal.EnrichmentProvider); Assert.Null(proposal.EnrichmentConfidence);
        Assert.Equal(1, proposal.MetadataSchemaVersion);
        var issues = await admin.GetFromJsonAsync<MetadataIssueView[]>("/api/knowledge/metadata/issues");
        Assert.Equal(proposal.Id, Assert.Single(issues!).ProposalId);
        Assert.Contains(issues![0].Issues, i => i.Field == "metadata_version");
        var preview = (await admin.GetFromJsonAsync<KnowledgeProposalPreview>($"/api/knowledge/proposals/{proposal.Id}/preview"))!;
        Assert.True(preview.RevisionMatches); Assert.Equal(before, preview.Before);
        Assert.Contains("title: 'Human title'", preview.After); Assert.Contains("# Human comment", preview.After);
        Assert.Contains("custom:\n  owner: Willem", preview.After); Assert.EndsWith("# Body\nKeep exact body.\n", preview.After);
        Assert.Equal(0, (await Process()).Applied);
        await Approve(admin, proposal);
        Assert.Equal(before, File.ReadAllText(Path.Combine(fixture.VaultPath, "human.md")));
        Assert.Equal(1, (await Process()).Applied);
        Assert.Equal(preview.After, File.ReadAllText(Path.Combine(fixture.VaultPath, "human.md")));
        Assert.Empty(await TestGit.RunAsync(fixture.VaultPath, "status", "--porcelain"));
        Assert.Equal("2", (await TestGit.RunAsync(fixture.VaultPath, "rev-list", "--count", "HEAD")).Trim());
        Assert.Equal(0, (await Scan(admin)).Proposed); // Metadata write changes the full hash, but not the body hash.
        Assert.Equal(1, (await Scan(admin)).Skipped);
        Assert.Empty((await admin.GetFromJsonAsync<MetadataIssueView[]>("/api/knowledge/metadata/issues"))!);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        foreach (var action in new[] { "brain.metadata.scan.started", "brain.metadata.issue.detected", "brain.metadata.enrichment.generated",
            "brain.metadata.proposal.created", "brain.metadata.proposal.approved", "brain.metadata.enrichment.applied" })
            Assert.True(await db.AuditEvents.AnyAsync(e => e.Action == action));
    }

    [Fact]
    public async Task Stale_metadata_revision_conflicts_instead_of_overwriting_a_newer_human_change()
    {
        Write("doc.md", "# Original body\n"); await TestGit.InitializeAsync(fixture.VaultPath);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin); var proposal = await SingleProposal(admin);
        await Approve(admin, proposal);
        Write("doc.md", "# Newer human content\nPreserve this.\n"); await TestGit.CommitAsync(fixture.VaultPath);
        Assert.Equal(1, (await Process()).Conflicts);
        Assert.Equal("# Newer human content\nPreserve this.\n", File.ReadAllText(Path.Combine(fixture.VaultPath, "doc.md")));
        var result = (await admin.GetFromJsonAsync<KnowledgeProposalView>($"/api/knowledge/proposals/{proposal.Id}", Json))!;
        Assert.Equal(KnowledgeProposalStatus.Conflict, result.Status);
        Assert.False((await admin.GetFromJsonAsync<KnowledgeProposalPreview>($"/api/knowledge/proposals/{proposal.Id}/preview"))!.RevisionMatches);
    }

    [Fact]
    public async Task Valid_metadata_is_skipped_and_invalid_or_future_yaml_is_reported_without_rewriting()
    {
        Write("valid.md", "---\nid: human\ntitle: Human\nmetadata_version: 1\n---\nBody");
        const string invalid = "---\nid: [unclosed\n---\nKeep malformed frontmatter.\n";
        Write("invalid.md", invalid);
        Write("future.md", "---\nid: future\ntitle: Future\nmetadata_version: 2\n---\nBody");
        Write("incomplete.md", "---\nid: incomplete\n---\nBody");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var result = await Scan(admin);
        Assert.Equal(4, result.Inspected); Assert.Equal(3, result.Issues); Assert.Equal(1, result.Proposed); Assert.Equal(0, result.Failed);
        var issues = (await admin.GetFromJsonAsync<MetadataIssueView[]>("/api/knowledge/metadata/issues"))!;
        Assert.Equal(3, issues.Length);
        Assert.Contains(issues, i => i.Path == "invalid.md" && i.ProposalId is null && i.Issues.Any(x => x.Code == "invalid_yaml"));
        Assert.Contains(issues, i => i.Path == "future.md" && i.ProposalId is null && i.Issues.Any(x => x.Code == "future_schema"));
        Assert.Equal(invalid, File.ReadAllText(Path.Combine(fixture.VaultPath, "invalid.md")));
        Assert.Equal(4, (await Scan(admin)).Skipped);
        File.Delete(Path.Combine(fixture.VaultPath, "invalid.md"));
        Assert.Equal(1, (await Scan(admin)).Removed);
    }

    [Fact]
    public async Task Semantic_enrichment_is_validated_preserves_human_metadata_and_never_auto_approves_high_confidence()
    {
        Write("doc.md", "---\nid: human\ntitle: Human title\ntype: procedure\ntags: [HumanTag]\n---\nBody");
        var provider = new FakeProvider("{\"type\":\"note\",\"summary\":\"Generated summary\",\"tags\":[\"generated\"],\"systems\":[\"coolify\"],\"confidence\":1}");
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await Service(scope.ServiceProvider, provider).ScanCoreAsync("metadata-enrichment-worker");
        Assert.Equal(1, result.Proposed); Assert.Equal(1, provider.Calls);
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        var proposal = await db.KnowledgeWriteProposals.AsNoTracking().SingleAsync();
        Assert.Equal(1d, proposal.EnrichmentConfidence); Assert.Equal(KnowledgeProposalStatus.Pending, proposal.Status); Assert.Null(proposal.ReviewerId);
        Assert.Equal("test-provider", proposal.EnrichmentProvider); Assert.Equal("model-v1", proposal.EnrichmentModel);
        Assert.Equal("metadata-enrichment-worker", proposal.AgentId);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var preview = (await admin.GetFromJsonAsync<KnowledgeProposalPreview>($"/api/knowledge/proposals/{proposal.Id}/preview"))!;
        Assert.Contains("title: Human title", preview.After); Assert.Contains("type: procedure", preview.After); Assert.Contains("tags: [HumanTag]", preview.After);
        Assert.Contains("Generated summary", preview.After); Assert.Contains("coolify", preview.After);
        Assert.Equal(0, (await Process()).Applied);
        var state = await db.MetadataScanStates.SingleAsync();
        Assert.NotNull(state.LastEnrichedAt); Assert.Equal("test-provider", state.Provider);
    }

    [Fact]
    public async Task Unchanged_and_rejected_inputs_are_not_reenriched_but_content_schema_and_model_changes_are()
    {
        Write("doc.md", "# Body\n");
        var provider = new FakeProvider("{\"type\":\"note\",\"confidence\":0.8}");
        using var scope = fixture.Factory.Services.CreateScope();
        var service = Service(scope.ServiceProvider, provider);
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Proposed);
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Skipped); Assert.Equal(1, provider.Calls);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await SingleProposal(admin);
        (await admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/reject", new ReviewKnowledgeProposalRequest(proposal.Version, "Human declined"))).EnsureSuccessStatusCode();
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Skipped); Assert.Equal(1, provider.Calls);
        Write("doc.md", "# Changed body\n");
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Proposed); Assert.Equal(2, provider.Calls);
        var upgraded = Service(scope.ServiceProvider, provider, new() { SchemaVersion = 2, SemanticEnabled = true });
        Assert.Equal(1, (await upgraded.ScanCoreAsync("metadata-enrichment-worker")).Proposed); Assert.Equal(3, provider.Calls);
        provider.Model = "model-v2";
        Assert.Equal(1, (await upgraded.ScanCoreAsync("metadata-enrichment-worker")).Proposed); Assert.Equal(4, provider.Calls);
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().KnowledgeWriteProposals.CountAsync());
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"confidence\":0.9,\"metadata_version\":999}")]
    [InlineData("{\"confidence\":0.9,\"type\":\"invalid\"}")]
    [InlineData("{\"confidence\":-1}")]
    public async Task Malformed_or_invalid_semantic_output_cannot_create_a_proposal(string json)
    {
        Write("doc.md", "# Body\n");
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await Service(scope.ServiceProvider, new FakeProvider(json)).ScanCoreAsync("metadata-enrichment-worker");
        Assert.Equal(1, result.Failed); Assert.Equal(0, result.Proposed);
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(0, await db.KnowledgeWriteProposals.CountAsync());
        Assert.NotNull((await db.MetadataScanStates.SingleAsync()).LastError);
        Assert.Equal("# Body\n", File.ReadAllText(Path.Combine(fixture.VaultPath, "doc.md")));
    }

    [Fact]
    public async Task Changes_during_provider_generation_are_rejected_then_retried_with_the_latest_revision()
    {
        Write("doc.md", "# Old\n");
        var provider = new FakeProvider("{\"confidence\":0.9}") { DuringCall = () => Write("doc.md", "# New\n") };
        using var scope = fixture.Factory.Services.CreateScope();
        var service = Service(scope.ServiceProvider, provider);
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Failed);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().KnowledgeWriteProposals.CountAsync());
        Assert.Equal(1, (await service.ScanCoreAsync("metadata-enrichment-worker")).Proposed);
        Assert.Equal("# New\n", File.ReadAllText(Path.Combine(fixture.VaultPath, "doc.md")));
    }

    [Fact]
    public async Task Administrative_permissions_worker_grants_and_generic_proposal_validation_are_enforced()
    {
        Write("doc.md", "# Body\n");
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/knowledge/metadata/issues")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsync("/api/knowledge/metadata/scan", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/knowledge/proposals", new
        { path = "doc.md", operation = "metadata_enrichment", content = "raw YAML is prohibited", reason = "Cannot bypass scanner" })).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/metadata-enrichment-worker/access", new UpdateAccessRequest([], [Permissions.KnowledgeRead]))).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal(403, (await Assert.ThrowsAsync<BrainException>(() => scope.ServiceProvider.GetRequiredService<MetadataService>()
            .ScanCoreAsync("metadata-enrichment-worker"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/knowledge/metadata/issues?limit=1001")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/knowledge/proposals?type=arbitrary")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/knowledge/proposals?status=1")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_scans_are_excluded_and_failed_state_proposal_transaction_rolls_back_everything()
    {
        Write("doc.md", "# Body\n");
        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMetadataScanStore>();
        await using (var lease = await store.TryAcquireAsync(default))
        {
            Assert.NotNull(lease);
            using var admin = fixture.Factory.Agent("willem-de-jong");
            Assert.True((await Scan(admin)).AlreadyRunning);
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(new MetadataScanState
        { Path = "doc.md", ContentHash = new string('a', 64), RevisionToken = "v1." + new string('a', 64), SchemaVersion = 1, PolicyHash = new string('b', 64) },
            new KnowledgeWriteProposal { AgentId = "not-a-real-agent", Path = "doc.md", Operation = KnowledgeOperation.MetadataEnrichment,
                Content = "{}", Reason = "Fail atomically" }, [new AuditEvent { AgentId = "test", Action = "must.not.persist", Resource = "test" }], default));
        using var verify = fixture.Factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(0, await db.MetadataScanStates.CountAsync()); Assert.Equal(0, await db.KnowledgeWriteProposals.CountAsync());
        Assert.False(await db.AuditEvents.AnyAsync(a => a.Action == "must.not.persist"));
    }

    private MetadataService Service(IServiceProvider services, FakeProvider provider, MetadataOptions? options = null)
    {
        options ??= new() { SemanticEnabled = true };
        var processor = new MetadataDocumentProcessor(options);
        var vault = services.GetRequiredService<IKnowledgeVault>();
        return new(vault, processor, services.GetRequiredService<IKnowledgeProvenance>(), provider,
            services.GetRequiredService<IMetadataScanStore>(), new KnowledgeProposalService(vault,
                services.GetRequiredService<IKnowledgeProposalStore>(), TimeProvider.System, processor),
            services.GetRequiredService<IAgentDirectory>(), services.GetRequiredService<IAuditLog>(), options, TimeProvider.System);
    }
    private void Write(string relative, string content)
    {
        var path = Path.Combine(fixture.VaultPath, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content);
    }
    private static async Task<MetadataScanResult> Scan(HttpClient client)
    {
        var response = await client.PostAsync("/api/knowledge/metadata/scan", null); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MetadataScanResult>())!;
    }
    private static async Task<KnowledgeProposalView> SingleProposal(HttpClient client) =>
        Assert.Single((await client.GetFromJsonAsync<KnowledgeProposalView[]>("/api/knowledge/proposals?type=metadata_enrichment", Json))!);
    private static async Task Approve(HttpClient client, KnowledgeProposalView proposal) =>
        (await client.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Reviewed metadata changes"))).EnsureSuccessStatusCode();
    private async Task<KnowledgeWriterResult> Process()
    {
        using var scope = fixture.Factory.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<KnowledgeWriterService>().ProcessAsync();
    }
    private sealed class FakeProvider(string json) : IMetadataEnrichmentProvider
    {
        public int Calls { get; private set; }
        public string Model { get; set; } = "model-v1";
        public Action? DuringCall { get; init; }
        public MetadataProviderInfo Info => new("test-provider", Model, Model);
        public Task<string> GenerateJsonAsync(MetadataEnrichmentInput input, CancellationToken ct)
        { Calls++; if (Calls == 1) DuringCall?.Invoke(); return Task.FromResult(json); }
    }
}
