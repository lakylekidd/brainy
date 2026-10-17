using Brain.Application;

namespace Brain.Api;

public static class GraphEndpoints
{
    public static void MapBrainGraph(this WebApplication app)
    {
        app.MapGet("/api/graph", (string? query, string? types, int? limit,
            RequestAgent actor, GraphService graph, CancellationToken ct) =>
            graph.SearchAsync(actor.Context, new(query, ParseTypes(types), limit ?? 80), ct));
        app.MapGet("/api/graph/search", (string query, string? types, int? limit,
            RequestAgent actor, GraphService graph, CancellationToken ct) =>
            graph.SearchAsync(actor.Context, new(query, ParseTypes(types), limit ?? 40), ct));
        app.MapGet("/api/graph/entity", (string id, RequestAgent actor, GraphService graph, CancellationToken ct) =>
            graph.GetAsync(actor.Context, id, ct));
        app.MapGet("/api/graph/neighbors", (string id, int? depth, int? limit, string? types,
            RequestAgent actor, GraphService graph, CancellationToken ct) =>
            graph.NeighborsAsync(actor.Context, id, depth ?? 1, limit ?? 80, ParseTypes(types), ct));
    }

    private static string[]? ParseTypes(string? types) => types?.Split(',');
}
