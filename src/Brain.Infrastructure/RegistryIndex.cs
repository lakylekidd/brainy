using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class RegistryIndexEntry
{
    public string Key { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string[] RequiredPermissions { get; set; } = [];
    public string[] RequiredTools { get; set; } = [];
    public string? Provider { get; set; }
    public string[] Capabilities { get; set; } = [];
    public string Source { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public Vector Embedding { get; set; } = null!;
    public string EmbeddingProvider { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public int EmbeddingDimensions { get; set; }
    public DateTimeOffset IndexedAt { get; set; }
}

public sealed class RegistryIndex(BrainDbContext db) : IRegistryIndex
{
    public async Task<IAsyncDisposable?> TryAcquireScanAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(601104204) AS \"Value\"").SingleAsync(ct)) return new Lease(db);
            await db.Database.CloseConnectionAsync();
            return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }

    public async Task<IReadOnlyList<RegistryIndexStatus>> StatusAsync(CancellationToken ct) =>
        await db.RegistryEntries.AsNoTracking().Select(e => new RegistryIndexStatus(e.Fingerprint,
            new EmbeddingModel(e.EmbeddingProvider, e.EmbeddingModel, e.EmbeddingDimensions))).ToListAsync(ct);

    public async Task ReplaceAsync(IReadOnlyList<EmbeddedRegistryDefinition> entries, EmbeddingModel model,
        DateTimeOffset indexedAt, AuditEvent audit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.RegistryEntries.ExecuteDeleteAsync(ct);
        db.RegistryEntries.AddRange(entries.Select(e => new RegistryIndexEntry
        {
            Key = e.Definition.Key, Fingerprint = e.Definition.Fingerprint, Kind = e.Definition.Kind == RegistryKind.Skill ? "skill" : "tool",
            Id = e.Definition.Id, Name = e.Definition.Name, Description = e.Definition.Description, Tags = e.Definition.Tags,
            RequiredPermissions = e.Definition.RequiredPermissions, RequiredTools = e.Definition.RequiredTools,
            Provider = e.Definition.Provider, Capabilities = e.Definition.Capabilities, Source = e.Definition.Source,
            ContentHash = e.Definition.ContentHash, Embedding = new Vector(e.Embedding),
            EmbeddingProvider = model.Provider, EmbeddingModel = model.Model, EmbeddingDimensions = model.Dimensions, IndexedAt = indexedAt
        }));
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<RegistryHit>> SearchAsync(string[] allowedFingerprints, float[] embedding,
        EmbeddingModel model, int limit, CancellationToken ct)
    {
        var vector = new Vector(embedding);
        var hits = await db.RegistryEntries.AsNoTracking().Where(e => allowedFingerprints.Contains(e.Fingerprint) &&
            e.EmbeddingProvider == model.Provider && e.EmbeddingModel == model.Model && e.EmbeddingDimensions == model.Dimensions)
            .OrderBy(e => e.Embedding.CosineDistance(vector)).ThenBy(e => e.Key).Take(limit)
            .Select(e => new { e.Fingerprint, Score = 1 - e.Embedding.CosineDistance(vector) }).ToListAsync(ct);
        return hits.Select(h => new RegistryHit(h.Fingerprint, Math.Clamp(h.Score, -1, 1))).ToArray();
    }

    private sealed class Lease(BrainDbContext context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(601104204)", CancellationToken.None); }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }
}
