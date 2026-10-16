using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class MemoryStore(BrainDbContext db) : IMemoryStore
{
    public async Task CreateAsync(AgentMemory memory, float[] embedding, EmbeddingModel model, AuditEvent audit, CancellationToken ct)
    {
        db.Memories.Add(memory);
        db.MemoryEmbeddings.Add(new MemoryEmbedding
        {
            MemoryId = memory.Id, Embedding = new Vector(embedding),
            Provider = model.Provider, Model = model.Model, Dimensions = model.Dimensions
        });
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct); // Memory, embedding and audit commit atomically.
    }

    public async Task<AgentMemory?> GetVisibleAsync(AgentContext agent, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        await Visible(agent, now).Where(m => m.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.ReferenceCount, m => m.ReferenceCount + 1)
            .SetProperty(m => m.LastAccessedAt, now), ct);
        return await Visible(agent, now).AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<ScoredMemory>> SearchAsync(AgentContext agent, MemorySearchRequest request,
        float[] embedding, EmbeddingModel model, DateTimeOffset now, CancellationToken ct)
    {
        var visible = Visible(agent, now).AsNoTracking();
        if (request.AgentId is not null) visible = visible.Where(m => m.AgentId == request.AgentId);
        if (request.MemoryType is not null) visible = visible.Where(m => m.MemoryType == request.MemoryType);
        if (request.Subject is not null) visible = visible.Where(m => m.Subject.ToLower().Contains(request.Subject.ToLower()));
        if (request.MinimumImportance is not null) visible = visible.Where(m => m.Importance >= request.MinimumImportance);
        if (request.MinimumConfidence is not null) visible = visible.Where(m => m.Confidence >= request.MinimumConfidence);
        var vector = new Vector(embedding);
        // Exact pgvector search initially: filtering before LIMIT has predictable recall for private/team scopes.
        var ranked = await (from m in visible
            join e in db.MemoryEmbeddings on m.Id equals e.MemoryId
            where e.Provider == model.Provider && e.Model == model.Model && e.Dimensions == model.Dimensions
            orderby e.Embedding.CosineDistance(vector), m.Id
            select new { Memory = m, Score = 1 - e.Embedding.CosineDistance(vector) })
            .Take(request.Limit).ToListAsync(ct);
        var ids = ranked.Select(r => r.Memory.Id).ToArray();
        await db.Memories.Where(m => ids.Contains(m.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.ReferenceCount, m => m.ReferenceCount + 1)
            .SetProperty(m => m.LastAccessedAt, now), ct);
        foreach (var row in ranked) { row.Memory.ReferenceCount++; row.Memory.LastAccessedAt = now; }
        return ranked.Select(r => new ScoredMemory(r.Memory, Math.Clamp(r.Score, -1, 1))).ToArray();
    }

    public async Task<bool> DeleteOwnedAsync(string agentId, Guid id, AuditEvent audit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var count = await db.Memories.Where(m => m.Id == id && m.AgentId == agentId).ExecuteDeleteAsync(ct);
        if (count == 0) return false;
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var count = await db.Memories.Where(m => m.ExpiresAt != null && m.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        if (count > 0)
        {
            db.AuditEvents.Add(new AuditEvent
            {
                AgentId = "brain-worker", Action = "brain.memory.expired", Resource = "memories", Timestamp = now,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { count })
            });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return count;
    }

    private IQueryable<AgentMemory> Visible(AgentContext agent, DateTimeOffset now) => db.Memories.Where(m =>
        (m.ExpiresAt == null || m.ExpiresAt > now) &&
        (m.AgentId == agent.Id || m.Visibility == MemoryVisibility.Organization ||
         (m.Visibility == MemoryVisibility.Team && agent.TeamId != null && m.TeamId == agent.TeamId)));
}
