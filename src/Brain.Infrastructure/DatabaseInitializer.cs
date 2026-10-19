using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(BrainDbContext db, bool seedDevelopment, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (seedDevelopment)
            // Serialize the idempotent development seed if two API instances start together.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(601104201)", ct);
        var permissions = await db.Permissions.ToListAsync(ct);
        foreach (var id in Permissions.All)
        {
            var permission = permissions.SingleOrDefault(p => p.Id == id);
            if (permission is null)
            {
                if (!seedDevelopment) continue;
                permission = new Permission { Id = id };
                db.Permissions.Add(permission);
                permissions.Add(permission);
            }
            permission.Description = Permissions.Descriptions[id];
        }
        if (!seedDevelopment)
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
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
        foreach (var permission in permissions.Where(p => p.Id is Permissions.ActivityRead or Permissions.ActivityLive or Permissions.TracesRead))
            if (member.Permissions.All(p => p.Id != permission.Id)) member.Permissions.Add(permission);
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
