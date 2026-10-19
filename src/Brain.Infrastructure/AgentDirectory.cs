using System.Text.Json;
using System.Text.RegularExpressions;
using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed partial class AgentDirectory(BrainDbContext db, TimeProvider time) : IAgentDirectory
{
    public async Task<AgentContext?> ResolveAsync(string id, CancellationToken ct)
    {
        var agent = await FullAgents().AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.Enabled, ct);
        return agent is null ? null : new AgentContext(agent.Id, agent.TeamId, Effective(agent).ToHashSet(StringComparer.Ordinal));
    }

    public async Task<IReadOnlyList<AgentView>> ListAsync(AgentContext actor, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsRead);
        return (await FullAgents().AsNoTracking().OrderBy(a => a.Id).Take(1000).ToListAsync(ct)).Select(View).ToArray();
    }

    public async Task<AgentView> GetAsync(AgentContext actor, string id, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsRead);
        var agent = await FullAgents().AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new BrainException(404, "Agent not found.");
        return View(agent);
    }

    public async Task<IReadOnlyList<RoleView>> ListRolesAsync(AgentContext actor, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsRead);
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Id).ToListAsync(ct);
        return roles.Select(r => new RoleView(r.Id, r.Description, r.Permissions.Select(p => p.Id).Order().ToArray())).ToArray();
    }

    public async Task<RoleView> CreateRoleAsync(AgentContext actor, CreateRoleRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsAdmin);
        Validation.Require(request.Id is not null && AgentIdPattern().IsMatch(request.Id), "Role id must be 1–100 lowercase letters, digits or hyphens.");
        Validation.Require(request.Description is not null && request.Description.Length <= 2000, "Description is too long.");
        Validation.Require(request.Permissions is not null && request.Permissions.Length <= 100
            && request.Permissions.Distinct(StringComparer.Ordinal).Count() == request.Permissions.Length, "Invalid permissions.");
        var permissionRows = await db.Permissions.Where(p => request.Permissions!.Contains(p.Id)).ToListAsync(ct);
        Validation.Require(permissionRows.Count == request.Permissions!.Length, "Unknown permission.");
        var role = new Role { Id = request.Id!, Description = request.Description!, Permissions = permissionRows };
        db.Roles.Add(role);
        Audit(actor.Id, "brain.role.created", role.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new BrainException(409, "Role already exists."); }
        return new RoleView(role.Id, role.Description, permissionRows.Select(p => p.Id).Order().ToArray());
    }

    public async Task<IReadOnlyList<PermissionView>> ListPermissionsAsync(AgentContext actor, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsRead);
        return await db.Permissions.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new PermissionView(p.Id, p.Description)).ToArrayAsync(ct);
    }

    public async Task<AgentView> CreateAsync(AgentContext actor, CreateAgentRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsAdmin);
        Validation.Require(request.Id is not null && AgentIdPattern().IsMatch(request.Id), "Agent id must be 1–100 lowercase letters, digits or hyphens.");
        Validation.Require(request.TeamId is null || AgentIdPattern().IsMatch(request.TeamId), "Invalid team id.");
        Validation.Require(request.Description is not null && request.Description.Length <= 2000, "Description is too long.");
        var agent = new Agent
        {
            Id = request.Id!, Name = Validation.Text(request.Name, "Name", 200), Description = request.Description!,
            TeamId = request.TeamId, MetadataJson = Validation.Metadata(request.Metadata),
            CreatedAt = time.GetUtcNow(), UpdatedAt = time.GetUtcNow()
        };
        await ApplyAccessAsync(agent, request.Roles ?? [], request.Permissions ?? [], ct);
        db.Agents.Add(agent);
        Audit(actor.Id, "brain.agent.created", agent.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new BrainException(409, "Agent already exists."); }
        return View(agent);
    }

    public async Task<AgentView> UpdateAccessAsync(AgentContext actor, string id, UpdateAccessRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.AgentsAdmin);
        // Keep the bootstrap administrator recoverable without storing credentials in this database.
        Validation.Require(id != actor.Id, "Administrators cannot change their own access through this endpoint.");
        var agent = await FullAgents().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new BrainException(404, "Agent not found.");
        await ApplyAccessAsync(agent, request.Roles, request.Permissions, ct);
        agent.Enabled = request.Enabled;
        agent.UpdatedAt = time.GetUtcNow();
        Audit(actor.Id, "brain.agent.access_updated", agent.Id);
        await db.SaveChangesAsync(ct);
        return View(agent);
    }

    private async Task ApplyAccessAsync(Agent agent, string[]? roles, string[]? permissions, CancellationToken ct)
    {
        Validation.Require(roles is not null && permissions is not null && roles.Length <= 100 && permissions.Length <= 100, "Invalid roles or permissions.");
        var roleRows = await db.Roles.Include(r => r.Permissions).Where(r => roles!.Contains(r.Id)).ToListAsync(ct);
        var permissionRows = await db.Permissions.Where(p => permissions!.Contains(p.Id)).ToListAsync(ct);
        Validation.Require(roleRows.Count == roles!.Distinct().Count() && permissionRows.Count == permissions!.Distinct().Count(), "Unknown role or permission.");
        agent.Roles.Clear(); agent.Roles.AddRange(roleRows);
        agent.Permissions.Clear(); agent.Permissions.AddRange(permissionRows);
    }

    private IQueryable<Agent> FullAgents() => db.Agents.Include(a => a.Permissions).Include(a => a.Roles).ThenInclude(r => r.Permissions).AsSplitQuery();
    private static IEnumerable<string> Effective(Agent agent) => agent.Permissions.Select(p => p.Id).Concat(agent.Roles.SelectMany(r => r.Permissions.Select(p => p.Id))).Distinct(StringComparer.Ordinal);
    private static AgentView View(Agent a) => new(a.Id, a.Name, a.Description, a.TeamId, a.Enabled,
        a.Roles.Select(r => r.Id).Order().ToArray(), Effective(a).Order().ToArray(),
        JsonSerializer.Deserialize<JsonElement>(a.MetadataJson), a.CreatedAt, a.UpdatedAt,
        a.Permissions.Select(p => p.Id).Order().ToArray());
    private void Audit(string actor, string action, string id) => db.AuditEvents.Add(new AuditEvent
    { AgentId = actor, Action = action, Resource = id, Timestamp = time.GetUtcNow() });
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9-]{0,99}\z")]
    private static partial Regex AgentIdPattern();
}
