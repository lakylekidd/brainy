using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;
    public int Ordinal { get; set; }
    public string Heading { get; set; } = "";
    public string Content { get; set; } = "";
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public Vector Embedding { get; set; } = null!;
}

public sealed class KnowledgeIndex(BrainDbContext db) : IKnowledgeIndex
{
    public async Task<IAsyncDisposable?> TryAcquireScanAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(601104202) AS \"Value\"").SingleAsync(ct);
            if (acquired) return new ScanLease(db);
            await db.Database.CloseConnectionAsync();
            return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> ListAsync(CancellationToken ct) =>
        await db.KnowledgeDocuments.AsNoTracking().ToListAsync(ct);
    public Task<KnowledgeDocument?> FindAsync(Guid id, CancellationToken ct) =>
        db.KnowledgeDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
    public Task<KnowledgeDocument?> FindByPathAsync(string path, CancellationToken ct) =>
        db.KnowledgeDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Path == path, ct);

    public async Task ReplaceAsync(KnowledgeDocument document, IReadOnlyList<EmbeddedKnowledgeChunk> chunks, AuditEvent audit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.KnowledgeDocuments.SingleOrDefaultAsync(d => d.Id == document.Id, ct);
        if (existing is null) db.KnowledgeDocuments.Add(document);
        else
        {
            db.Entry(existing).CurrentValues.SetValues(document);
            await db.KnowledgeChunks.Where(c => c.DocumentId == document.Id).ExecuteDeleteAsync(ct);
        }
        db.KnowledgeChunks.AddRange(chunks.Select(c => new KnowledgeChunk
        {
            DocumentId = document.Id, Ordinal = c.Chunk.Ordinal, Heading = c.Chunk.Heading,
            Content = c.Chunk.Content, StartLine = c.Chunk.StartLine, EndLine = c.Chunk.EndLine, Embedding = new Vector(c.Embedding)
        }));
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear(); // Bound tracked memory during a long vault scan.
    }

    public async Task RemoveAsync(Guid id, AuditEvent audit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.KnowledgeDocuments.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(KnowledgeSearchRequest request,
        float[] vector, EmbeddingModel model, CancellationToken ct)
    {
        var query = db.KnowledgeChunks.AsNoTracking().Where(c => c.Document.EmbeddingProvider == model.Provider &&
            c.Document.EmbeddingModel == model.Model && c.Document.EmbeddingDimensions == model.Dimensions);
        if (request.PathPrefix is not null)
        {
            var prefix = request.PathPrefix + "/";
            query = query.Where(c => c.Document.Path == request.PathPrefix || c.Document.Path.StartsWith(prefix));
        }
        var embedding = new Vector(vector);
        var hits = await query.OrderBy(c => c.Embedding.CosineDistance(embedding)).ThenBy(c => c.Document.Path).ThenBy(c => c.Ordinal)
            .Take(request.Limit).Select(c => new
            {
                c.Id, c.Content, c.Heading, c.StartLine, c.EndLine,
                c.Document, Score = 1 - c.Embedding.CosineDistance(embedding)
            }).ToListAsync(ct);
        return hits.Select(c => new KnowledgeSearchResult("knowledge", c.Id, c.Document.Title, c.Content,
            Math.Clamp(c.Score, -1, 1), c.Document.Path,
            new(c.Document.Id, c.Heading, c.StartLine, c.EndLine, c.Document.RevisionToken, c.Document.ContentHash, c.Document.IndexedAt))).ToArray();
    }

    private sealed class ScanLease(BrainDbContext context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(601104202)", CancellationToken.None); }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }
}
