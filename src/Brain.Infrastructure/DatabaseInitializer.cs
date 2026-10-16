using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(BrainDbContext db, bool seedDevelopment, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (!seedDevelopment) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize the idempotent development seed if two API instances start together.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(601104201)", ct);
        var permissions = await db.Permissions.ToListAsync(ct);
        foreach (var id in Permissions.All.Where(id => permissions.All(p => p.Id != id)))
        {
            var permission = new Permission { Id = id, Description = id };
            db.Permissions.Add(permission); permissions.Add(permission);
        }
        var admin = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == "administrator", ct);
        if (admin is null)
        {
            admin = new Role { Id = "administrator", Description = "Homelab Brain administrator", Permissions = permissions.ToList() };
            db.Roles.Add(admin);
        }
        var member = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == "agent", ct);
        if (member is null)
        {
            member = new Role { Id = "agent", Description = "Memory access within visibility scope",
                Permissions = permissions.Where(p => p.Id.StartsWith("memory.", StringComparison.Ordinal)).ToList() };
            db.Roles.Add(member);
        }
        // Extend existing development seeds when a module introduces new permissions.
        foreach (var permission in permissions)
            if (admin.Permissions.All(p => p.Id != permission.Id)) admin.Permissions.Add(permission);
        var knowledgeRead = permissions.Single(p => p.Id == Permissions.KnowledgeRead);
        if (member.Permissions.All(p => p.Id != Permissions.KnowledgeRead)) member.Permissions.Add(knowledgeRead);
        var knowledgePropose = permissions.Single(p => p.Id == Permissions.KnowledgePropose);
        if (member.Permissions.All(p => p.Id != Permissions.KnowledgePropose)) member.Permissions.Add(knowledgePropose);
        var registryRead = permissions.Single(p => p.Id == Permissions.RegistryRead);
        if (member.Permissions.All(p => p.Id != Permissions.RegistryRead)) member.Permissions.Add(registryRead);
        foreach (var (id, name, role) in new[] { ("willem-de-jong", "Willem de Jong", admin), ("codex", "Codex", member) })
            if (!await db.Agents.AnyAsync(a => a.Id == id, ct))
                db.Agents.Add(new Agent { Id = id, Name = name, TeamId = "platform", Roles = [role],
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        if (!await db.Agents.AnyAsync(a => a.Id == "metadata-enrichment-worker", ct))
            db.Agents.Add(new Agent { Id = "metadata-enrichment-worker", Name = "Metadata enrichment worker", TeamId = "platform",
                Permissions = permissions.Where(p => p.Id is Permissions.KnowledgeRead or Permissions.KnowledgePropose).ToList(),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
