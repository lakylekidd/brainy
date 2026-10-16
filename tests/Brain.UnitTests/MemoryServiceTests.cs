using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;

namespace Brain.UnitTests;

public sealed class MemoryServiceTests
{
    private static readonly AgentContext Writer = new("codex", "platform", new HashSet<string>
        { Permissions.MemoryRead, Permissions.MemoryWrite, Permissions.MemoryDelete, Permissions.MemoryShare });

    [Fact]
    public async Task Creation_uses_authenticated_owner_and_team_and_defaults_to_private()
    {
        var store = new RecordingStore();
        var service = new MemoryService(store, new DevelopmentEmbeddingProvider(), new RecordingAudit(), TimeProvider.System);
        var result = await service.RememberAsync(Writer, new(MemoryType.Fact, "Cluster", "Corosync is missing."));
        Assert.Equal("codex", result.AgentId);
        Assert.Equal(MemoryVisibility.Private, result.Visibility);
        Assert.Null(result.TeamId);
        Assert.Equal(384, store.Vector!.Length);
        Assert.Equal("brain.memory.created", store.Audit!.Action);
        Assert.Equal(PromotionStatus.None, result.PromotionStatus);
    }

    [Fact]
    public async Task Missing_permission_fails_before_embedding_or_persistence()
    {
        var store = new RecordingStore();
        var service = new MemoryService(store, new FailingEmbedding(), new RecordingAudit(), TimeProvider.System);
        var denied = new AgentContext("read-only", null, new HashSet<string> { Permissions.MemoryRead });
        var error = await Assert.ThrowsAsync<BrainException>(() => service.RememberAsync(denied, new(MemoryType.Fact, "test", "content")));
        Assert.Equal(403, error.StatusCode);
        Assert.Null(store.Vector);
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(0.5, 1.1)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(0.5, double.PositiveInfinity)]
    public async Task Invalid_scores_are_rejected(double importance, double confidence)
    {
        var service = Service();
        var error = await Assert.ThrowsAsync<BrainException>(() => service.RememberAsync(Writer,
            new(MemoryType.Fact, "subject", "content", Importance: importance, Confidence: confidence)));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Team_sharing_requires_server_assigned_team()
    {
        var error = await Assert.ThrowsAsync<BrainException>(() => Service().RememberAsync(Writer with { TeamId = null },
            new(MemoryType.Fact, "subject", "content", MemoryVisibility.Team)));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Publishing_requires_share_permission()
    {
        var actor = Writer with { Permissions = new HashSet<string> { Permissions.MemoryWrite } };
        var error = await Assert.ThrowsAsync<BrainException>(() => Service().RememberAsync(actor,
            new(MemoryType.Fact, "subject", "content", MemoryVisibility.Organization)));
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task Empty_query_and_invalid_enum_are_rejected()
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => Service().SearchAsync(Writer, new("  ")))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => Service().RememberAsync(Writer,
            new((MemoryType)999, "subject", "content")))).StatusCode);
    }

    [Fact]
    public async Task Provider_vectors_must_match_descriptor_and_be_nonzero()
    {
        var service = new MemoryService(new RecordingStore(), new InvalidEmbedding(), new RecordingAudit(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RememberAsync(Writer, new(MemoryType.Fact, "subject", "content")));
    }

    [Fact]
    public async Task Development_vectors_are_repeatable_normalized_and_have_overlap_ranking()
    {
        var provider = new DevelopmentEmbeddingProvider();
        var query = await provider.EmbedAsync("Proxmox cluster corosync", default);
        var relevant = await provider.EmbedAsync("Corosync cluster configuration on Proxmox", default);
        var unrelated = await provider.EmbedAsync("Chocolate cookie recipe", default);
        Assert.Equal(query, await provider.EmbedAsync("PROXMOX CLUSTER COROSYNC", default));
        Assert.InRange(query.Sum(v => (double)v * v), 0.99999, 1.00001);
        Assert.True(query.Zip(relevant).Sum(p => p.First * p.Second) > query.Zip(unrelated).Sum(p => p.First * p.Second));
        Assert.Contains(await provider.EmbedAsync("!!!", default), v => v != 0);
    }

    private static MemoryService Service() => new(new RecordingStore(), new DevelopmentEmbeddingProvider(), new RecordingAudit(), TimeProvider.System);

    private sealed class FailingEmbedding : IEmbeddingProvider
    {
        public EmbeddingModel Model => new("test", "unreachable", 384);
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) => throw new InvalidOperationException("Must not run");
    }
    private sealed class InvalidEmbedding : IEmbeddingProvider
    {
        public EmbeddingModel Model => new("test", "invalid", 384);
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new float[384]);
    }
    private sealed class RecordingAudit : IAuditLog
    {
        public Task WriteAsync(AuditEvent audit, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditEvent>> ListAsync(AgentContext actor, int limit, CancellationToken ct) => throw new NotImplementedException();
    }
    private sealed class RecordingStore : IMemoryStore
    {
        public float[]? Vector { get; private set; }
        public AuditEvent? Audit { get; private set; }
        public Task CreateAsync(AgentMemory memory, float[] embedding, EmbeddingModel model, AuditEvent audit, CancellationToken ct)
        { Vector = embedding; Audit = audit; return Task.CompletedTask; }
        public Task<AgentMemory?> GetVisibleAsync(AgentContext agent, Guid id, DateTimeOffset now, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ScoredMemory>> SearchAsync(AgentContext agent, MemorySearchRequest request, float[] embedding, EmbeddingModel model, DateTimeOffset now, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> DeleteOwnedAsync(string agentId, Guid id, AuditEvent audit, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct) => throw new NotImplementedException();
    }
}
