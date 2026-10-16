using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class MetadataScanStore(BrainDbContext db) : IMetadataScanStore
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(601104205) AS \"Value\"").SingleAsync(ct)) return new Lease(db);
            await db.Database.CloseConnectionAsync(); return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }
    public Task<MetadataScanState?> FindAsync(string path, CancellationToken ct) =>
        db.MetadataScanStates.AsNoTracking().SingleOrDefaultAsync(s => s.Path == path, ct);
    public async Task<IReadOnlyList<MetadataScanState>> IssuesAsync(int limit, CancellationToken ct) =>
        await db.MetadataScanStates.AsNoTracking().Where(s => s.IssuesJson != "[]" || s.LastError != null)
            .OrderBy(s => s.Path).Take(limit).ToListAsync(ct);

    public async Task SaveAsync(MetadataScanState state, KnowledgeWriteProposal? proposal, IReadOnlyList<AuditEvent> events, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.MetadataScanStates.SingleOrDefaultAsync(s => s.Path == state.Path, ct);
        if (existing is null) db.MetadataScanStates.Add(state);
        else db.Entry(existing).CurrentValues.SetValues(state);
        if (proposal is not null) db.KnowledgeWriteProposals.Add(proposal);
        db.AuditEvents.AddRange(events);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        finally { db.ChangeTracker.Clear(); }
    }
    public Task<int> RemoveMissingAsync(IReadOnlyList<string> paths, CancellationToken ct) =>
        db.MetadataScanStates.Where(s => !paths.Contains(s.Path)).ExecuteDeleteAsync(ct);

    private sealed class Lease(BrainDbContext context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(601104205)", CancellationToken.None); }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }
}
