using System.Security.Cryptography;
using System.Text;
using Brain.Domain;

namespace Brain.Application;

public sealed record GraphReference(string Kind, string Id);
public sealed record GraphEntity(string Id, string Type, string Label, GraphReference Source,
    IReadOnlyDictionary<string, string> Metadata);
public sealed record GraphRelationship(string Id, string Type, string Source, string Target)
{
    public static GraphRelationship Create(string type, string source, string target) =>
        new("relationship:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\n{type}\n{target}"))).ToLowerInvariant(),
            type, source, target);
}
public sealed record GraphResult(IReadOnlyList<GraphEntity> Entities, IReadOnlyList<GraphRelationship> Relationships,
    bool Truncated, int Depth = 0);
public sealed record GraphEntityPage(IReadOnlyList<GraphEntity> Entities, bool Truncated);
public sealed record GraphQuery(string? Query = null, string[]? Types = null, int Limit = 80);

public interface IGraphSource
{
    Task<GraphEntityPage> SearchAsync(AgentContext actor, GraphQuery query, CancellationToken ct);
    Task<GraphEntity?> FindAsync(AgentContext actor, string id, CancellationToken ct);
    Task<GraphResult> NeighborsAsync(AgentContext actor, string id, string[]? types, int limit, CancellationToken ct);
    Task<IReadOnlyList<GraphRelationship>> RelationshipsAsync(AgentContext actor, IReadOnlyList<GraphEntity> entities, CancellationToken ct);
}

public interface IGraphReferenceExtractor
{
    string[] Extract(string markdown);
}

public static class GraphIds
{
    public static readonly string[] Types = ["document", "skill", "tool", "agent", "role", "permission"];
    public static bool IsValid(string? id)
    {
        if (id is null || id.Length > 1100 || id.Any(char.IsControl)) return false;
        var separator = id.IndexOf(':');
        if (separator <= 0 || !Types.Contains(id[..separator], StringComparer.Ordinal)) return false;
        var key = id[(separator + 1)..];
        return key.Length > 0 && key.Trim() == key && !key.Contains('\\') &&
            key.Split('/').All(part => part.Length > 0 && part is not "." and not "..");
    }
}
