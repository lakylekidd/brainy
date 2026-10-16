using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class KnowledgeApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Index_search_and_canonical_read_return_sections_source_and_revision()
    {
        const string source = "# Proxmox cluster\nCluster overview.\n## Recovery\nRestore corosync.conf on nacht-02 before joining the Proxmox cluster.\n";
        Write("Infrastructure/nacht-02.md", source);
        Write("Procedures/cookies.md", "# Cookies\nBake chocolate cookies.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var scan = await Scan(admin);
        Assert.Equal(2, scan.Indexed);
        Assert.Empty(scan.Failures);
        using var reader = fixture.Factory.Agent("codex");
        var hits = await Search(reader, new("restore corosync nacht 02", "Infrastructure", 1));
        var hit = Assert.Single(hits);
        Assert.Equal("Infrastructure/nacht-02.md", hit.Source);
        Assert.Contains("Restore corosync", hit.Content);
        Assert.Equal("Proxmox cluster / Recovery", hit.Metadata.Heading);
        Assert.Equal(3, hit.Metadata.StartLine);
        Assert.InRange(hit.Score, 0.1, 1);
        var read = await reader.GetFromJsonAsync<KnowledgeDocumentView>($"/api/knowledge/documents/{hit.Metadata.DocumentId}");
        Assert.Equal(source, read!.Content);
        Assert.True(read.IndexCurrent);
        Assert.Equal(hit.Metadata.RevisionToken, read.RevisionToken);
        Assert.Equal(source, File.ReadAllText(Path.Combine(fixture.VaultPath, hit.Source)));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(2, await db.KnowledgeDocuments.CountAsync());
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "brain.knowledge.read" && a.AgentId == "codex"));
        Assert.Equal(384, (await db.KnowledgeChunks.FirstAsync()).Embedding.ToArray().Length);
    }

    [Fact]
    public async Task Content_changes_refresh_chunks_and_tokens_while_unchanged_documents_are_skipped()
    {
        var file = Write("Services/brain.md", "# Brain\nInitial knowledge.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        var before = await ReadPath(admin, "Services/brain.md");
        var skipped = await Scan(admin);
        Assert.Equal(0, skipped.Indexed);
        Assert.Equal(1, skipped.Unchanged);
        File.WriteAllText(file, "# Brain\nUpdated knowledge with corosync recovery instructions.");
        // Reads are canonical, and stale cached snippets are hidden even before the next scan.
        var pending = await ReadPath(admin, "Services/brain.md");
        Assert.False(pending.IndexCurrent);
        Assert.NotEqual(before.RevisionToken, pending.RevisionToken);
        Assert.Empty(await Search(admin, new("Initial knowledge")));
        var updated = await Scan(admin);
        Assert.Equal(1, updated.Indexed);
        var after = await ReadPath(admin, "Services/brain.md");
        Assert.Equal(before.Id, after.Id);
        Assert.True(after.IndexCurrent);
        Assert.Equal(pending.RevisionToken, after.RevisionToken);
        var hit = Assert.Single(await Search(admin, new("corosync recovery")));
        Assert.DoesNotContain("Initial knowledge", hit.Content);
    }

    [Fact]
    public async Task Deleted_documents_are_hidden_immediately_and_pruned_with_their_chunks()
    {
        var file = Write("Incidents/old.md", "# Old incident\nCorosync outage.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        var before = await ReadPath(admin, "Incidents/old.md");
        File.Delete(file);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/knowledge/documents/{before.Id}")).StatusCode);
        Assert.Empty(await Search(admin, new("corosync")));
        Assert.Equal(1, (await Scan(admin)).Removed);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(0, await db.KnowledgeDocuments.CountAsync());
        Assert.Equal(0, await db.KnowledgeChunks.CountAsync());
    }

    [Fact]
    public async Task Permission_checks_and_path_validation_apply_to_every_knowledge_operation()
    {
        Write("public.md", "# Shared knowledge");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var agent = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsync("/api/knowledge/scan", null)).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.MemoryRead]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/knowledge/search", new KnowledgeSearchRequest("shared"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/knowledge/document?path=public.md")).StatusCode);
        foreach (var path in new[] { "../outside.md", "C:/outside.md", ".git/config.md", "folder\\outside.md" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/knowledge/document?path=" + Uri.EscapeDataString(path))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/knowledge/search", new KnowledgeSearchRequest("shared", "../"))).StatusCode);
    }

    [Fact]
    public async Task Scan_lock_excludes_a_second_scanner_and_releases_after_completion()
    {
        using var firstScope = fixture.Factory.Services.CreateScope();
        await using (var lease = await firstScope.ServiceProvider.GetRequiredService<IKnowledgeIndex>().TryAcquireScanAsync(default))
        {
            Assert.NotNull(lease);
            using var admin = fixture.Factory.Agent("willem-de-jong");
            Assert.True((await Scan(admin)).AlreadyRunning);
        }
        using var next = fixture.Factory.Agent("willem-de-jong");
        Assert.False((await Scan(next)).AlreadyRunning);
    }

    [Fact]
    public async Task Model_and_chunker_changes_trigger_reindex_and_bad_files_do_not_erase_good_documents()
    {
        Write("good.md", "# Good\nProxmox recovery.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.KnowledgeDocuments.ExecuteUpdateAsync(s => s.SetProperty(d => d.EmbeddingModel, "obsolete"));
        Assert.Empty(await Search(admin, new("recovery")));
        Assert.Equal(1, (await Scan(admin)).Indexed);
        await db.KnowledgeDocuments.ExecuteUpdateAsync(s => s.SetProperty(d => d.ChunkerVersion, "obsolete"));
        Assert.Equal(1, (await Scan(admin)).Indexed);
        File.WriteAllBytes(Path.Combine(fixture.VaultPath, "invalid.md"), [0xC3, 0x28]);
        var result = await Scan(admin);
        Assert.Equal("invalid.md", Assert.Single(result.Failures).Path);
        Assert.Equal(1, result.Unchanged);
        Assert.Single(await Search(admin, new("recovery")));
    }

    [Fact]
    public async Task A_file_changed_during_embedding_is_not_published_and_retries_successfully()
    {
        var path = Write("changing.md", "# Changing\nInitial state.");
        using var scope = fixture.Factory.Services.CreateScope();
        var service = new KnowledgeService(scope.ServiceProvider.GetRequiredService<IKnowledgeVault>(),
            scope.ServiceProvider.GetRequiredService<IKnowledgeIndex>(), new MarkdownChunker(),
            new MutatingProvider(() => File.WriteAllText(path, "# Changing\nLatest state.")),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(), TimeProvider.System);
        var result = await service.ScanCoreAsync("brain-worker");
        Assert.Equal(0, result.Indexed);
        Assert.Single(result.Failures);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().KnowledgeDocuments.CountAsync());
        using var admin = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(1, (await Scan(admin)).Indexed);
        Assert.Contains("Latest state", (await ReadPath(admin, "changing.md")).Content);
    }

    [Fact]
    public async Task Failed_chunk_replacement_rolls_back_both_metadata_and_old_chunks()
    {
        Write("stable.md", "# Stable\nOriginal section.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        var before = await ReadPath(admin, "stable.md");
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var index = scope.ServiceProvider.GetRequiredService<IKnowledgeIndex>();
            var document = (await index.FindAsync(before.Id!.Value, default))!;
            document.Title = "Must roll back";
            await Assert.ThrowsAsync<DbUpdateException>(() => index.ReplaceAsync(document,
                [new(new(0, "invalid", "invalid", 1, 1), [1f, 0f])],
                new AuditEvent { AgentId = "brain-worker", Action = "must.not.persist", Resource = "stable.md", Timestamp = DateTimeOffset.UtcNow }, default));
        }
        var after = await ReadPath(admin, "stable.md");
        Assert.Equal(before.RevisionToken, after.RevisionToken);
        var hit = Assert.Single(await Search(admin, new("Original section")));
        Assert.Equal("Stable", hit.Title);
        Assert.Contains("Original section", hit.Content);
    }

    [Fact]
    public async Task An_unavailable_vault_aborts_before_deleting_existing_index_rows()
    {
        Write("retained.md", "# Retained\nKeep this index.");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        using var scope = fixture.Factory.Services.CreateScope();
        var unavailable = new FileSystemKnowledgeVault(new() { VaultPath = Path.Combine(fixture.VaultPath, "not-mounted") });
        var service = new KnowledgeService(unavailable, scope.ServiceProvider.GetRequiredService<IKnowledgeIndex>(),
            new MarkdownChunker(), new DevelopmentEmbeddingProvider(), scope.ServiceProvider.GetRequiredService<IAuditLog>(), TimeProvider.System);
        Assert.Equal(503, (await Assert.ThrowsAsync<BrainException>(() => service.ScanCoreAsync("brain-worker"))).StatusCode);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().KnowledgeDocuments.CountAsync());
        Assert.False((await Scan(admin)).AlreadyRunning); // Failed scans release their advisory lease too.
    }

    private sealed class MutatingProvider(Action mutate) : IEmbeddingProvider
    {
        private readonly DevelopmentEmbeddingProvider inner = new();
        public EmbeddingModel Model => inner.Model;
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        { mutate(); return inner.EmbedAsync(text, cancellationToken); }
    }

    private string Write(string path, string content)
    {
        var full = Path.Combine(fixture.VaultPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }
    private static async Task<KnowledgeScanResult> Scan(HttpClient client)
    {
        var response = await client.PostAsync("/api/knowledge/scan", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeScanResult>())!;
    }
    private static async Task<KnowledgeSearchResult[]> Search(HttpClient client, KnowledgeSearchRequest query)
    {
        var response = await client.PostAsJsonAsync("/api/knowledge/search", query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeSearchResult[]>())!;
    }
    private static async Task<KnowledgeDocumentView> ReadPath(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<KnowledgeDocumentView>("/api/knowledge/document?path=" + Uri.EscapeDataString(path)))!;
}
