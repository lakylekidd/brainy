using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class GraphSource(BrainDbContext db, IRegistrySource registry, IKnowledgeVault vault) : IGraphSource
{
    private IReadOnlyList<RegistryDefinition>? definitions;

    public async Task<GraphEntityPage> SearchAsync(AgentContext actor, GraphQuery query, CancellationToken ct)
    {
        var nodes = new List<GraphEntity>();
        var take = query.Limit + 1;
        var truncated = false;
        var term = query.Query?.Trim().ToLowerInvariant() ?? "";
        if (Allows(actor, "document", query.Types))
        {
            var rows = await db.KnowledgeDocuments.AsNoTracking()
                .Where(d => term == "" || d.Title.ToLower().Contains(term) || d.Path.ToLower().Contains(term))
                .OrderBy(d => d.Path).Take(take).ToArrayAsync(ct);
            truncated |= rows.Length == take;
            foreach (var row in rows)
                if (await CurrentAsync(row, ct)) nodes.Add(Document(row));
        }
        if (Allows(actor, "skill", query.Types) || Allows(actor, "tool", query.Types))
            nodes.AddRange((await CatalogAsync(actor, ct)).Where(e => Includes(query.Types, Kind(e)) &&
                (e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || e.Id.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(e => e.Key, StringComparer.Ordinal).Take(take).Select(Registry));
        if (Allows(actor, "agent", query.Types))
            nodes.AddRange((await db.Agents.AsNoTracking()
                .Where(a => term == "" || a.Id.ToLower().Contains(term) || a.Name.ToLower().Contains(term))
                .OrderBy(a => a.Id).Take(take).ToArrayAsync(ct)).Select(Agent));
        if (Allows(actor, "role", query.Types))
            nodes.AddRange((await db.Roles.AsNoTracking().Where(r => term == "" || r.Id.ToLower().Contains(term))
                .OrderBy(r => r.Id).Take(take).ToArrayAsync(ct)).Select(Role));
        if (Allows(actor, "permission", query.Types))
            nodes.AddRange((await db.Permissions.AsNoTracking().Where(p => term == "" || p.Id.ToLower().Contains(term))
                .OrderBy(p => p.Id).Take(take).ToArrayAsync(ct)).Select(Permission));
        // Interleave types so the initial bounded overview is not crowded out by a large vault.
        var groups = nodes.GroupBy(n => n.Type).Select(g => g.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray()).ToArray();
        var ordered = Enumerable.Range(0, groups.Length == 0 ? 0 : groups.Max(g => g.Length))
            .SelectMany(i => groups.Where(g => i < g.Length).Select(g => g[i])).Take(take).ToArray();
        return new(ordered.Take(query.Limit).ToArray(), truncated || ordered.Length > query.Limit);
    }

    public async Task<GraphEntity?> FindAsync(AgentContext actor, string id, CancellationToken ct)
    {
        var (type, key) = Split(id);
        if (!Allows(actor, type, null)) return null;
        switch (type)
        {
            case "document":
                var document = await db.KnowledgeDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Path == key, ct);
                return document is not null && await CurrentAsync(document, ct) ? Document(document) : null;
            case "skill":
            case "tool":
                var entry = (await CatalogAsync(actor, ct)).SingleOrDefault(e => e.Key == id);
                return entry is null ? null : Registry(entry);
            case "agent":
                var agent = await db.Agents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == key, ct);
                return agent is null ? null : Agent(agent);
            case "role":
                var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == key, ct);
                return role is null ? null : Role(role);
            case "permission":
                var permission = await db.Permissions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == key, ct);
                return permission is null ? null : Permission(permission);
            default: return null;
        }
    }

    public async Task<GraphResult> NeighborsAsync(AgentContext actor, string id, string[]? types, int limit, CancellationToken ct)
    {
        var root = await FindAsync(actor, id, ct);
        if (root is null) return new([], [], false);
        var candidates = new Dictionary<string, GraphEntity>(StringComparer.Ordinal);
        var take = limit + 1;
        var truncated = false;
        foreach (var edge in await RelationshipsFromAsync(actor, [root], ct))
        {
            var (type, _) = Split(edge.Target);
            if (!Includes(types, type) || candidates.ContainsKey(edge.Target)) continue;
            var node = await FindAsync(actor, edge.Target, ct);
            if (node is not null) candidates.TryAdd(node.Id, node);
        }
        if (Allows(actor, "document", types))
        {
            var documents = await db.KnowledgeDocuments.AsNoTracking().Where(d => d.GraphReferences.Contains(id))
                .OrderBy(d => d.Path).Take(take).ToArrayAsync(ct);
            truncated |= documents.Length == take;
            foreach (var row in documents)
                if (await CurrentAsync(row, ct)) candidates.TryAdd("document:" + row.Path, Document(row));
        }
        if (Allows(actor, "skill", types) || Allows(actor, "tool", types))
            foreach (var entry in (await CatalogAsync(actor, ct)).Where(e => Includes(types, Kind(e)) &&
                (e.RequiredTools.Any(t => "tool:" + t == id) || e.RequiredPermissions.Any(p => "permission:" + p == id))))
                candidates.TryAdd(entry.Key, Registry(entry));
        var (rootType, key) = Split(id);
        if (Allows(actor, "agent", types) && rootType is "role" or "permission")
        {
            var agents = db.Agents.AsNoTracking().Where(a => rootType == "role"
                ? a.Roles.Any(r => r.Id == key) : a.Permissions.Any(p => p.Id == key));
            foreach (var row in await agents.OrderBy(a => a.Id).Take(take).ToArrayAsync(ct))
                candidates.TryAdd("agent:" + row.Id, Agent(row));
        }
        if (Allows(actor, "role", types) && rootType == "permission")
            foreach (var row in await db.Roles.AsNoTracking().Where(r => r.Permissions.Any(p => p.Id == key))
                .OrderBy(r => r.Id).Take(take).ToArrayAsync(ct))
                candidates.TryAdd("role:" + row.Id, Role(row));
        candidates.Remove(id);
        return new(candidates.Values.OrderBy(n => n.Id, StringComparer.Ordinal).Take(limit).ToArray(), [], truncated || candidates.Count > limit);
    }

    public async Task<IReadOnlyList<GraphRelationship>> RelationshipsAsync(AgentContext actor,
        IReadOnlyList<GraphEntity> entities, CancellationToken ct)
    {
        var ids = entities.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        return (await RelationshipsFromAsync(actor, entities, ct))
            .Where(e => ids.Contains(e.Source) && ids.Contains(e.Target))
            .DistinctBy(e => e.Id).OrderBy(e => e.Id, StringComparer.Ordinal)
            .Take(GraphService.MaximumRelationships + 1).ToArray();
    }

    private async Task<IReadOnlyList<GraphRelationship>> RelationshipsFromAsync(AgentContext actor,
        IReadOnlyList<GraphEntity> entities, CancellationToken ct)
    {
        var edges = new List<GraphRelationship>();
        var documents = Keys(entities, "document");
        if (actor.Has(Permissions.KnowledgeRead) && documents.Length > 0)
            foreach (var row in await db.KnowledgeDocuments.AsNoTracking().Where(d => documents.Contains(d.Path)).ToArrayAsync(ct))
                foreach (var target in row.GraphReferences)
                    edges.Add(GraphRelationship.Create("references", "document:" + row.Path, target));
        var registryIds = entities.Where(n => n.Type is "skill" or "tool").Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        if (actor.Has(Permissions.RegistryRead) && registryIds.Count > 0)
            foreach (var entry in (await CatalogAsync(actor, ct)).Where(e => registryIds.Contains(e.Key)))
            {
                foreach (var tool in entry.RequiredTools)
                    edges.Add(GraphRelationship.Create("requires_tool", entry.Key, "tool:" + tool));
                foreach (var permission in entry.RequiredPermissions)
                    edges.Add(GraphRelationship.Create("requires_permission", entry.Key, "permission:" + permission));
            }
        var agents = Keys(entities, "agent");
        if (actor.Has(Permissions.AgentsRead) && agents.Length > 0)
            foreach (var row in await db.Agents.AsNoTracking().Include(a => a.Roles).Include(a => a.Permissions)
                .AsSplitQuery().Where(a => agents.Contains(a.Id)).ToArrayAsync(ct))
            {
                foreach (var role in row.Roles)
                    edges.Add(GraphRelationship.Create("has_role", "agent:" + row.Id, "role:" + role.Id));
                foreach (var permission in row.Permissions)
                    edges.Add(GraphRelationship.Create("grants_permission", "agent:" + row.Id, "permission:" + permission.Id));
            }
        var roles = Keys(entities, "role");
        if (actor.Has(Permissions.AgentsRead) && roles.Length > 0)
            foreach (var row in await db.Roles.AsNoTracking().Include(r => r.Permissions)
                .Where(r => roles.Contains(r.Id)).ToArrayAsync(ct))
                foreach (var permission in row.Permissions)
                    edges.Add(GraphRelationship.Create("grants_permission", "role:" + row.Id, "permission:" + permission.Id));
        return edges;
    }

    private async Task<IReadOnlyList<RegistryDefinition>> CatalogAsync(AgentContext actor, CancellationToken ct)
    {
        definitions ??= await registry.ReadAsync(ct);
        var keys = RegistryAccess.AllowedKeys(actor, definitions);
        return definitions.Where(e => keys.Contains(e.Key)).ToArray();
    }

    private async Task<bool> CurrentAsync(KnowledgeDocument document, CancellationToken ct)
    {
        try { return (await vault.ReadAsync(document.Path, ct)).RevisionToken == document.RevisionToken; }
        catch (BrainException e) when (e.StatusCode is 400 or 404 or 413 or 422) { return false; }
    }

    private static bool Allows(AgentContext actor, string type, string[]? types) => Includes(types, type) && actor.Has(type switch
    {
        "document" => Permissions.KnowledgeRead,
        "skill" or "tool" => Permissions.RegistryRead,
        _ => Permissions.AgentsRead
    });
    private static bool Includes(string[]? types, string type) => types is null || types.Contains(type, StringComparer.Ordinal);
    private static (string Type, string Key) Split(string id) => (id[..id.IndexOf(':')], id[(id.IndexOf(':') + 1)..]);
    private static string[] Keys(IReadOnlyList<GraphEntity> entities, string type) =>
        entities.Where(n => n.Type == type).Select(n => n.Source.Id).ToArray();
    private static string Kind(RegistryDefinition entry) => entry.Kind == RegistryKind.Skill ? "skill" : "tool";
    private static GraphEntity Document(KnowledgeDocument document) => new("document:" + document.Path, "document",
        document.Title, new("knowledge_document", document.Path), new Dictionary<string, string>
        { ["path"] = document.Path, ["indexedAt"] = document.IndexedAt.ToString("O") });
    private static GraphEntity Registry(RegistryDefinition entry) => new(entry.Key, Kind(entry), entry.Name,
        new("registry_manifest", entry.Source), entry.Provider is null ? new Dictionary<string, string>() :
            new Dictionary<string, string> { ["provider"] = entry.Provider });
    private static GraphEntity Agent(Agent agent) => new("agent:" + agent.Id, "agent", agent.Name,
        new("agent", agent.Id), new Dictionary<string, string> { ["enabled"] = agent.Enabled ? "true" : "false" });
    private static GraphEntity Role(Role role) => new("role:" + role.Id, "role", role.Id, new("role", role.Id), new Dictionary<string, string>());
    private static GraphEntity Permission(Permission permission) => new("permission:" + permission.Id, "permission",
        permission.Id, new("permission", permission.Id), new Dictionary<string, string>());
}
