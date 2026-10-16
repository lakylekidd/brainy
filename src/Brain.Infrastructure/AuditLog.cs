using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class AuditLog(BrainDbContext db) : IAuditLog
{
    public async Task WriteAsync(AuditEvent audit, CancellationToken ct)
    {
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAsync(AgentContext actor, int limit, CancellationToken ct)
    {
        actor.Require(Permissions.AuditRead);
        Validation.Require(limit is >= 1 and <= 1000, "Limit must be between 1 and 1000.");
        return await db.AuditEvents.AsNoTracking().OrderByDescending(a => a.Timestamp).Take(limit).ToListAsync(ct);
    }
}
