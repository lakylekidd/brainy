using Brain.Domain;

namespace Brain.Application;

public interface ICurrentAgent
{
    AgentContext Context { get; }
}

public sealed record BrainSearchRequest(string Query, int Limit = 10);
public sealed record SearchSourceFailure(string Source, int Status, string Detail);
public sealed record BrainSearchResult(IReadOnlyList<KnowledgeSearchResult> Knowledge,
    IReadOnlyList<MemorySearchResult> Memories, IReadOnlyList<RegistryResult> Skills,
    IReadOnlyList<RegistryResult> Tools, IReadOnlyList<SearchSourceFailure> Failures);

public sealed class SearchService(MemoryService memories, KnowledgeService knowledge, RegistryService registry)
{
    public async Task<BrainSearchResult> SearchAsync(AgentContext actor, BrainSearchRequest request, CancellationToken ct = default)
    {
        Validation.Text(request.Query, "Query", 4000);
        Validation.Require(request.Limit is >= 1 and <= 100, "Limit must be between 1 and 100.");
        if (!actor.Has(Permissions.MemoryRead) && !actor.Has(Permissions.KnowledgeRead) && !actor.Has(Permissions.RegistryRead))
            throw new BrainException(403, "The agent lacks a search permission.");
        IReadOnlyList<MemorySearchResult> memoryHits = [];
        IReadOnlyList<KnowledgeSearchResult> knowledgeHits = [];
        CapabilitySearchResult capabilities = new([], []);
        var failures = new List<SearchSourceFailure>();
        // These services share a scoped DbContext, so searches must execute sequentially.
        if (actor.Has(Permissions.MemoryRead))
            memoryHits = await memories.SearchAsync(actor, new(request.Query, Limit: request.Limit), ct);
        if (actor.Has(Permissions.KnowledgeRead))
        {
            try { knowledgeHits = await knowledge.SearchAsync(actor, new(request.Query, Limit: request.Limit), ct); }
            catch (BrainException e) when (e.StatusCode is 422 or 503) { failures.Add(new("knowledge", e.StatusCode, e.Message)); }
        }
        if (actor.Has(Permissions.RegistryRead))
        {
            try { capabilities = await registry.FindCapabilitiesAsync(actor, new(request.Query, Limit: request.Limit), ct); }
            catch (BrainException e) when (e.StatusCode is 422 or 503) { failures.Add(new("registry", e.StatusCode, e.Message)); }
        }
        return new(knowledgeHits, memoryHits, capabilities.Skills, capabilities.Tools, failures);
    }
}
