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

public sealed class KnowledgeProposalApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false) } };
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        File.WriteAllText(Path.Combine(fixture.VaultPath, "doc.md"), "# Cluster\n## Recovery\nOriginal body.\n## Status\nKeep status.\n");
        await TestGit.InitializeAsync(fixture.VaultPath);
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Propose_preview_approve_and_writer_apply_are_separate_audited_steps()
    {
        using var agent = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var before = await Read(agent);
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Observed missing corosync.conf.", "Troubleshooting", before.RevisionToken));
        Assert.Equal(KnowledgeProposalStatus.Pending, proposal.Status);
        Assert.Equal("codex", proposal.AgentId);
        var preview = await agent.GetFromJsonAsync<KnowledgeProposalPreview>($"/api/knowledge/proposals/{proposal.Id}/preview");
        Assert.True(preview!.RevisionMatches);
        Assert.Equal(before.Content, preview.Before);
        Assert.EndsWith("Observed missing corosync.conf.", preview.After);
        Assert.Equal(before.Content, (await Read(agent)).Content);
        Assert.Equal(0, (await Process()).Applied); // Pending proposals are never auto-approved.
        var approved = await Approve(admin, proposal);
        Assert.Equal(KnowledgeProposalStatus.Approved, approved.Status);
        Assert.Equal(before.Content, (await Read(agent)).Content); // HTTP approval has no filesystem writer.
        Assert.Equal(1, (await Process()).Applied);
        var applied = await Get(agent, proposal.Id);
        Assert.Equal(KnowledgeProposalStatus.Applied, applied.Status);
        Assert.NotNull(applied.GitCommit);
        Assert.NotEqual(before.RevisionToken, (await Read(agent)).RevisionToken);
        Assert.Equal("2", (await TestGit.RunAsync(fixture.VaultPath, "rev-list", "--count", "HEAD")).Trim());
        Assert.Empty(await TestGit.RunAsync(fixture.VaultPath, "status", "--porcelain"));
        Assert.Contains("Agent: codex", await TestGit.RunAsync(fixture.VaultPath, "log", "-1", "--format=%B"));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Null((await db.KnowledgeWriteProposals.SingleAsync()).ResultContent);
        foreach (var action in new[] { "brain.knowledge.proposed", "brain.knowledge.approved", "brain.knowledge.prepared", "brain.knowledge.applied" })
            Assert.True(await db.AuditEvents.AnyAsync(e => e.Action == action));
        Assert.Equal(0, (await Process()).Applied);
    }

    [Fact]
    public async Task Stale_approved_proposal_is_a_conflict_and_cannot_overwrite_newer_knowledge()
    {
        using var agent = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Stale observation.", "Test", (await Read(agent)).RevisionToken));
        await Approve(admin, proposal);
        File.WriteAllText(Path.Combine(fixture.VaultPath, "doc.md"), "# Newer canonical knowledge\nPreserve me.");
        await TestGit.CommitAsync(fixture.VaultPath);
        var head = await TestGit.RunAsync(fixture.VaultPath, "rev-parse", "HEAD");
        Assert.Equal(1, (await Process()).Conflicts);
        Assert.Equal(KnowledgeProposalStatus.Conflict, (await Get(agent, proposal.Id)).Status);
        Assert.Equal("# Newer canonical knowledge\nPreserve me.", (await Read(agent)).Content);
        Assert.Equal(head, await TestGit.RunAsync(fixture.VaultPath, "rev-parse", "HEAD"));
        Assert.False((await agent.GetFromJsonAsync<KnowledgeProposalPreview>($"/api/knowledge/proposals/{proposal.Id}/preview"))!.RevisionMatches);
    }

    [Fact]
    public async Task Two_proposals_against_the_same_revision_produce_one_commit_and_one_conflict()
    {
        using var agent = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var token = (await Read(agent)).RevisionToken;
        var first = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "First.", "First observation", token));
        var second = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Second.", "Second observation", token));
        await Approve(admin, first); await Approve(admin, second);
        var result = await Process();
        Assert.Equal(1, result.Applied); Assert.Equal(1, result.Conflicts);
        Assert.Contains("First.", (await Read(agent)).Content);
        Assert.DoesNotContain("Second.", (await Read(agent)).Content);
    }

    [Fact]
    public async Task Creation_and_section_replacement_commit_only_the_approved_document()
    {
        using var agent = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var created = await Propose(agent, new("Incidents/new.md", KnowledgeOperation.CreateDocument, "# Incident\nDetails.", "New incident"));
        Assert.False(Directory.Exists(Path.Combine(fixture.VaultPath, "Incidents")));
        await Approve(admin, created);
        Assert.Equal(1, (await Process()).Applied);
        Assert.Equal("Incidents/new.md", (await TestGit.RunAsync(fixture.VaultPath, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD")).Trim());
        var before = await Read(agent);
        var replaced = await Propose(agent, new("doc.md", KnowledgeOperation.ReplaceSection, "Verified recovery procedure.", "Updated procedure", before.RevisionToken, "Cluster / Recovery"));
        await Approve(admin, replaced);
        Assert.Equal(1, (await Process()).Applied);
        var after = await Read(agent);
        Assert.Contains("## Recovery\nVerified recovery procedure.\n## Status\nKeep status.", after.Content);
        Assert.DoesNotContain("Original body", after.Content);
        Assert.Equal("doc.md", (await TestGit.RunAsync(fixture.VaultPath, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD")).Trim());
    }

    [Fact]
    public async Task Create_collision_and_deleted_source_become_conflicts_without_touching_files()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var create = await Propose(agent, new("new.md", KnowledgeOperation.CreateDocument, "# Proposed", "Create"));
        var append = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Never recreate.", "Append", (await Read(agent)).RevisionToken));
        await Approve(admin, create); await Approve(admin, append);
        File.WriteAllText(Path.Combine(fixture.VaultPath, "new.md"), "# Human creation");
        File.Delete(Path.Combine(fixture.VaultPath, "doc.md"));
        await TestGit.CommitAsync(fixture.VaultPath);
        Assert.Equal(2, (await Process()).Conflicts);
        Assert.Equal("# Human creation", File.ReadAllText(Path.Combine(fixture.VaultPath, "new.md")));
        Assert.False(File.Exists(Path.Combine(fixture.VaultPath, "doc.md")));
    }

    [Fact]
    public async Task Review_and_permissions_are_deterministic_and_proposal_visibility_is_scoped()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Private proposal.", "Observation", (await Read(agent)).RevisionToken));
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Self approve"))).StatusCode);
        (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Other", Permissions: [Permissions.KnowledgeRead, Permissions.KnowledgePropose]))).EnsureSuccessStatusCode();
        using var other = fixture.Factory.Agent("other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/knowledge/proposals/{proposal.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<KnowledgeProposalView[]>("/api/knowledge/proposals", Json))!);
        Assert.Single((await admin.GetFromJsonAsync<KnowledgeProposalView[]>("/api/knowledge/proposals", Json))!);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.KnowledgeRead]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/knowledge/proposals",
            new ProposeKnowledgeChangeRequest("new.md", KnowledgeOperation.CreateDocument, "# New", "Test"), Json)).StatusCode);
        await Approve(admin, proposal);
        Assert.Equal(1, (await Process()).Rejected); // Revoked proposers cannot start a new prepared write.
        Assert.DoesNotContain("Private proposal", (await Read(admin)).Content);
    }

    [Fact]
    public async Task Concurrent_approve_reject_uses_proposal_versions_and_only_one_review_wins()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Observation.", "Test", (await Read(agent)).RevisionToken));
        var outcomes = await Task.WhenAll(
            admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Approve")),
            admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/reject", new ReviewKnowledgeProposalRequest(proposal.Version, "Reject")));
        Assert.Single(outcomes, o => o.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, o => o.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Stale review"))).StatusCode);
    }

    [Fact]
    public async Task Rejected_proposals_are_never_written_and_invalid_requests_are_not_queued()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var before = await Read(agent);
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Rejected.", "Test", before.RevisionToken));
        (await admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/reject", new ReviewKnowledgeProposalRequest(proposal.Version, "Unverified claim"))).EnsureSuccessStatusCode();
        Assert.Equal(0, (await Process()).Applied);
        Assert.Equal(before.Content, (await Read(agent)).Content);
        foreach (var request in new[]
        {
            new ProposeKnowledgeChangeRequest("../outside.md", KnowledgeOperation.CreateDocument, "# Escape", "Test"),
            new(".git/attack.md", KnowledgeOperation.CreateDocument, "# Escape", "Test"),
            new("new.md", KnowledgeOperation.CreateDocument, "# New", "Test", before.RevisionToken),
            new("doc.md", KnowledgeOperation.Append, "New", "Test"),
            new("doc.md", KnowledgeOperation.ReplaceSection, "New", "Test", before.RevisionToken, "Missing")
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/knowledge/proposals", request, Json)).StatusCode);
        File.AppendAllText(Path.Combine(fixture.VaultPath, "doc.md"), "Changed.");
        Assert.Equal(HttpStatusCode.Conflict, (await agent.PostAsJsonAsync("/api/knowledge/proposals",
            new ProposeKnowledgeChangeRequest("doc.md", KnowledgeOperation.Append, "Stale", "Test", before.RevisionToken), Json)).StatusCode);
        Assert.Single((await agent.GetFromJsonAsync<KnowledgeProposalView[]>("/api/knowledge/proposals", Json))!);
    }

    [Fact]
    public async Task Dirty_checkout_is_deferred_and_a_later_retry_preserves_human_commits()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Observation.", "Test", (await Read(agent)).RevisionToken));
        await Approve(admin, proposal);
        File.WriteAllText(Path.Combine(fixture.VaultPath, "human.md"), "# Uncommitted human document");
        Assert.Equal(1, (await Process()).Deferred);
        Assert.Equal(KnowledgeProposalStatus.Approved, (await Get(agent, proposal.Id)).Status);
        await TestGit.CommitAsync(fixture.VaultPath);
        Assert.Equal(1, (await Process()).Applied);
        Assert.Equal("# Uncommitted human document", File.ReadAllText(Path.Combine(fixture.VaultPath, "human.md")));
        Assert.Empty(await TestGit.RunAsync(fixture.VaultPath, "status", "--porcelain"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Git_success_database_failure_is_recovered_without_duplicate_append_or_commit(bool rowVersionConflict)
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Exactly once.", "Test", (await Read(agent)).RevisionToken));
        await Approve(admin, proposal);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var failing = new FailAppliedStore(scope.ServiceProvider.GetRequiredService<IKnowledgeProposalStore>(), rowVersionConflict);
            var service = new KnowledgeWriterService(failing, scope.ServiceProvider.GetRequiredService<ICanonicalKnowledgeWriter>(),
                scope.ServiceProvider.GetRequiredService<IAgentDirectory>(), TimeProvider.System);
            if (rowVersionConflict) await Assert.ThrowsAsync<ProposalConcurrencyException>(() => service.ProcessAsync());
            else await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync());
        }
        Assert.Equal(KnowledgeProposalStatus.Validating, (await Get(agent, proposal.Id)).Status);
        Assert.Equal(1, (await Process()).Applied);
        Assert.Equal(KnowledgeProposalStatus.Applied, (await Get(agent, proposal.Id)).Status);
        Assert.Equal(1, (await Read(agent)).Content.Split("Exactly once.").Length - 1);
        Assert.Equal("2", (await TestGit.RunAsync(fixture.VaultPath, "rev-list", "--count", "HEAD")).Trim());
    }

    [Fact]
    public async Task Writer_lease_serializes_workers_and_a_published_prepared_commit_is_recoverable()
    {
        using var agent = fixture.Factory.Agent("codex"); using var admin = fixture.Factory.Agent("willem-de-jong");
        var proposal = await Propose(agent, new("doc.md", KnowledgeOperation.Append, "Recovery.", "Test", (await Read(agent)).RevisionToken));
        await Approve(admin, proposal);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IKnowledgeProposalStore>();
            await using var lease = await store.TryAcquireWriterAsync(default);
            Assert.NotNull(lease);
            Assert.True((await Process()).AlreadyRunning);
            var row = (await store.FindAsync(proposal.Id, default))!;
            var prepared = await scope.ServiceProvider.GetRequiredService<ICanonicalKnowledgeWriter>().PrepareAsync(row, default);
            TestGit.SetPrepared(row, prepared);
            await store.SaveAsync(row, new AuditEvent { AgentId = "codex", Action = "test.prepared", Resource = row.Id.ToString(), Timestamp = DateTimeOffset.UtcNow }, default);
            await TestGit.RunAsync(fixture.VaultPath, "update-ref", prepared.Branch, prepared.Commit, prepared.ParentCommit);
        }
        Assert.DoesNotContain("Recovery.", (await Read(agent)).Content);
        Assert.Equal(1, (await Process()).Applied);
        Assert.Contains("Recovery.", (await Read(agent)).Content);
        Assert.Empty(await TestGit.RunAsync(fixture.VaultPath, "status", "--porcelain"));
    }

    private async Task<KnowledgeWriterResult> Process()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeWriterService>().ProcessAsync();
    }
    private static async Task<KnowledgeProposalView> Propose(HttpClient client, ProposeKnowledgeChangeRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/knowledge/proposals", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeProposalView>(Json))!;
    }
    private static async Task<KnowledgeProposalView> Approve(HttpClient client, KnowledgeProposalView proposal)
    {
        var response = await client.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Verified evidence"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeProposalView>(Json))!;
    }
    private static async Task<KnowledgeProposalView> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<KnowledgeProposalView>($"/api/knowledge/proposals/{id}", Json))!;
    private static async Task<KnowledgeDocumentView> Read(HttpClient client) =>
        (await client.GetFromJsonAsync<KnowledgeDocumentView>("/api/knowledge/document?path=doc.md"))!;

    private sealed class FailAppliedStore(IKnowledgeProposalStore inner, bool rowVersionConflict) : IKnowledgeProposalStore
    {
        public Task CreateAsync(KnowledgeWriteProposal p, AuditEvent audit, CancellationToken ct) => inner.CreateAsync(p, audit, ct);
        public Task<KnowledgeWriteProposal?> FindAsync(Guid id, CancellationToken ct) => inner.FindAsync(id, ct);
        public Task<IReadOnlyList<KnowledgeWriteProposal>> ListAsync(string? agent, KnowledgeProposalStatus? status, int limit, CancellationToken ct, bool? metadataEnrichment = null) => inner.ListAsync(agent, status, limit, ct, metadataEnrichment);
        public Task SaveAsync(KnowledgeWriteProposal p, AuditEvent audit, CancellationToken ct) => p.Status == KnowledgeProposalStatus.Applied
            ? (rowVersionConflict ? throw new ProposalConcurrencyException() : throw new InvalidOperationException("Simulated database failure after Git success"))
            : inner.SaveAsync(p, audit, ct);
        public Task<IAsyncDisposable?> TryAcquireWriterAsync(CancellationToken ct) => inner.TryAcquireWriterAsync(ct);
        public Task<IReadOnlyList<KnowledgeWriteProposal>> QueuedAsync(int limit, CancellationToken ct) => inner.QueuedAsync(limit, ct);
    }
}
