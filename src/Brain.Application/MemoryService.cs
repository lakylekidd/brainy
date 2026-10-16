using Brain.Domain;

namespace Brain.Application;

public sealed class MemoryService(IMemoryStore store, IEmbeddingProvider embeddings, IAuditLog audit, TimeProvider time)
{
    public async Task<MemoryView> RememberAsync(AgentContext agent, RememberRequest request, CancellationToken ct = default)
    {
        agent.Require(Permissions.MemoryWrite);
        Validation.Require(Enum.IsDefined(request.MemoryType) && Enum.IsDefined(request.Visibility), "Unknown memory type or visibility.");
        var subject = Validation.Text(request.Subject, "Subject", 300);
        var content = Validation.Text(request.Content, "Content", 32000);
        Validation.Score(request.Importance, "Importance");
        Validation.Score(request.Confidence, "Confidence");
        var now = time.GetUtcNow();
        Validation.Require(request.ExpiresAt is null || request.ExpiresAt > now, "Expiration must be in the future.");
        Validation.Require(request.Source is null || request.Source.Length <= 2000, "Source is too long.");
        if (request.Visibility != MemoryVisibility.Private) agent.Require(Permissions.MemoryShare);
        Validation.Require(request.Visibility != MemoryVisibility.Team || agent.TeamId is not null, "Team visibility requires a team.");
        var memory = new AgentMemory
        {
            AgentId = agent.Id, Subject = subject, Content = content, MemoryType = request.MemoryType,
            Importance = request.Importance, Confidence = request.Confidence, Visibility = request.Visibility,
            TeamId = request.Visibility == MemoryVisibility.Team ? agent.TeamId : null,
            CreatedAt = now, UpdatedAt = now, ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
            Source = request.Source, MetadataJson = Validation.Metadata(request.Metadata)
        };
        var vector = await EmbedAsync($"{subject}\n{content}", ct);
        await store.CreateAsync(memory, vector, embeddings.Model, Event(agent.Id, "brain.memory.created", memory.Id.ToString()), ct);
        return MemoryView.From(memory);
    }

    public async Task<MemoryView> GetAsync(AgentContext agent, Guid id, CancellationToken ct = default)
    {
        agent.Require(Permissions.MemoryRead);
        var memory = await store.GetVisibleAsync(agent, id, time.GetUtcNow(), ct)
            ?? throw new BrainException(404, "Memory not found.");
        await audit.WriteAsync(Event(agent.Id, "brain.memory.read", id.ToString()), ct);
        return MemoryView.From(memory);
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(AgentContext agent, MemorySearchRequest request, CancellationToken ct = default)
    {
        agent.Require(Permissions.MemoryRead);
        Validation.Text(request.Query, "Query", 4000);
        Validation.Require(request.Limit is >= 1 and <= 100, "Limit must be between 1 and 100.");
        Validation.Require(request.MemoryType is null || Enum.IsDefined(request.MemoryType.Value), "Unknown memory type.");
        Validation.Require(request.Subject is null || request.Subject.Length <= 300, "Subject is too long.");
        if (request.MinimumImportance is { } importance) Validation.Score(importance, "Minimum importance");
        if (request.MinimumConfidence is { } confidence) Validation.Score(confidence, "Minimum confidence");
        var vector = await EmbedAsync(request.Query, ct);
        var matches = await store.SearchAsync(agent, request, vector, embeddings.Model, time.GetUtcNow(), ct);
        await audit.WriteAsync(Event(agent.Id, "brain.memory.searched", "memories"), ct);
        return matches.Select(m => new MemorySearchResult("memory", m.Memory.Id, m.Memory.Subject,
            m.Memory.Content, m.Score, m.Memory.Source, MemoryView.From(m.Memory))).ToArray();
    }

    public async Task DeleteAsync(AgentContext agent, Guid id, CancellationToken ct = default)
    {
        agent.Require(Permissions.MemoryDelete);
        if (!await store.DeleteOwnedAsync(agent.Id, id, Event(agent.Id, "brain.memory.deleted", id.ToString()), ct))
            throw new BrainException(404, "Memory not found.");
    }

    private async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var vector = await embeddings.EmbedAsync(text, ct);
        if (vector.Length != embeddings.Model.Dimensions || vector.Any(v => !float.IsFinite(v)) || !vector.Any(v => v != 0))
            throw new InvalidOperationException("Embedding provider returned an invalid vector.");
        return vector;
    }

    private AuditEvent Event(string actor, string action, string resource) => new()
    { AgentId = actor, Action = action, Resource = resource, Timestamp = time.GetUtcNow() };
}
