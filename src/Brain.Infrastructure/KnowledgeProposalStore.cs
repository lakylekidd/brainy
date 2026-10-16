using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class KnowledgeProposalStore(BrainDbContext db) : IKnowledgeProposalStore
{
    public async Task CreateAsync(KnowledgeWriteProposal proposal, AuditEvent audit, CancellationToken ct)
    {
        db.KnowledgeWriteProposals.Add(proposal); db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    }
    public Task<KnowledgeWriteProposal?> FindAsync(Guid id, CancellationToken ct) =>
        db.KnowledgeWriteProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
    public async Task<IReadOnlyList<KnowledgeWriteProposal>> ListAsync(string? agentId, KnowledgeProposalStatus? status, int limit, CancellationToken ct, bool? metadataEnrichment = null) =>
        await db.KnowledgeWriteProposals.AsNoTracking().Where(p => (agentId == null || p.AgentId == agentId) &&
            (status == null || p.Status == status) && (metadataEnrichment == null || (p.Operation == KnowledgeOperation.MetadataEnrichment) == metadataEnrichment))
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(limit).ToListAsync(ct);
    public async Task<IReadOnlyList<KnowledgeWriteProposal>> QueuedAsync(int limit, CancellationToken ct) =>
        await db.KnowledgeWriteProposals.AsNoTracking().Where(p => p.Status == KnowledgeProposalStatus.Validating || p.Status == KnowledgeProposalStatus.Approved)
            .OrderByDescending(p => p.Status == KnowledgeProposalStatus.Validating).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id).Take(limit).ToListAsync(ct);

    public async Task SaveAsync(KnowledgeWriteProposal proposal, AuditEvent audit, CancellationToken ct)
    {
        var oldVersion = proposal.Version;
        proposal.Version++;
        db.Attach(proposal).State = EntityState.Modified;
        db.Entry(proposal).Property(p => p.Version).OriginalValue = oldVersion;
        db.AuditEvents.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ProposalConcurrencyException(); }
        finally { db.ChangeTracker.Clear(); }
    }

    public async Task<IAsyncDisposable?> TryAcquireWriterAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(601104203) AS \"Value\"").SingleAsync(ct))
                return new WriterLease(db);
            await db.Database.CloseConnectionAsync(); return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }
    private sealed class WriterLease(BrainDbContext context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(601104203)", CancellationToken.None); }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }
}
