using Brain.Domain;

namespace Brain.Application;

public sealed class GraphService(IGraphSource source)
{
    public const int MaximumEntities = 200;
    public const int MaximumRelationships = 400;
    public const int MaximumDepth = 2;

    public async Task<GraphResult> SearchAsync(AgentContext actor, GraphQuery query, CancellationToken ct = default)
    {
        RequireAccess(actor);
        Validate(query);
        var page = await source.SearchAsync(actor, query, ct);
        var relationships = await source.RelationshipsAsync(actor, page.Entities, ct);
        return new(page.Entities, relationships.Take(MaximumRelationships).ToArray(),
            page.Truncated || relationships.Count > MaximumRelationships);
    }

    public async Task<GraphEntity> GetAsync(AgentContext actor, string id, CancellationToken ct = default)
    {
        RequireAccess(actor);
        Validation.Require(GraphIds.IsValid(id), "Invalid graph entity id.");
        return await source.FindAsync(actor, id, ct) ?? throw new BrainException(404, "Graph entity not found.");
    }

    public async Task<GraphResult> NeighborsAsync(AgentContext actor, string id, int depth = 1,
        int limit = 80, string[]? types = null, CancellationToken ct = default)
    {
        RequireAccess(actor);
        Validate(new(Types: types, Limit: limit));
        Validation.Require(depth is >= 0 and <= MaximumDepth, "Depth must be between 0 and 2.");
        var root = await GetAsync(actor, id, ct);
        var entities = new Dictionary<string, GraphEntity>(StringComparer.Ordinal) { [root.Id] = root };
        var frontier = new List<string> { root.Id };
        var truncated = false;
        for (var level = 0; level < depth && frontier.Count > 0; level++)
        {
            var next = new List<string>();
            foreach (var current in frontier)
            {
                if (entities.Count == limit) { truncated = true; break; }
                var adjacent = await source.NeighborsAsync(actor, current, types, limit, ct);
                truncated |= adjacent.Truncated;
                foreach (var entity in adjacent.Entities)
                {
                    if (entities.ContainsKey(entity.Id)) continue;
                    if (entities.Count == limit) { truncated = true; continue; }
                    entities.Add(entity.Id, entity);
                    next.Add(entity.Id);
                }
            }
            frontier = next;
        }
        var nodes = entities.Values.ToArray();
        var relationships = await source.RelationshipsAsync(actor, nodes, ct);
        return new(nodes, relationships.Take(MaximumRelationships).ToArray(),
            truncated || relationships.Count > MaximumRelationships, depth);
    }

    private static void RequireAccess(AgentContext actor)
    {
        if (!actor.Has(Permissions.KnowledgeRead) && !actor.Has(Permissions.RegistryRead) && !actor.Has(Permissions.AgentsRead))
            throw new BrainException(403, "A graph source permission is required.");
    }

    private static void Validate(GraphQuery query)
    {
        Validation.Require(query.Limit is >= 1 and <= MaximumEntities, "Limit must be between 1 and 200.");
        Validation.Require(query.Query is null || query.Query.Length <= 200, "Graph query exceeds 200 characters.");
        Validation.Require(query.Types is null || (query.Types.Length is > 0 and <= 6 &&
            query.Types.All(type => GraphIds.Types.Contains(type, StringComparer.Ordinal))), "Unknown graph entity type.");
    }
}
