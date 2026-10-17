using Brain.Domain;

namespace Brain.Application;

public sealed class KnowledgeService(IKnowledgeVault vault, IKnowledgeIndex index, IMarkdownChunker chunker,
    IEmbeddingProvider embeddings, IAuditLog audit, TimeProvider time, IGraphReferenceExtractor graphReferences)
{
    public async Task<KnowledgeDocumentView> ReadAsync(AgentContext agent, Guid id, CancellationToken ct = default)
    {
        agent.Require(Permissions.KnowledgeRead);
        var document = await index.FindAsync(id, ct) ?? throw new BrainException(404, "Document not found.");
        return await ReadCoreAsync(agent, document.Path, document, ct);
    }

    public async Task<KnowledgeDocumentView> ReadPathAsync(AgentContext agent, string path, CancellationToken ct = default)
    {
        agent.Require(Permissions.KnowledgeRead);
        // The vault validates the caller's path before it is used for any filesystem access.
        var snapshot = await vault.ReadAsync(path, ct);
        return await ViewAsync(agent, snapshot, await index.FindByPathAsync(snapshot.Path, ct), ct);
    }

    private async Task<KnowledgeDocumentView> ReadCoreAsync(AgentContext agent, string path, KnowledgeDocument document, CancellationToken ct) =>
        await ViewAsync(agent, await vault.ReadAsync(path, ct), document, ct);

    private async Task<KnowledgeDocumentView> ViewAsync(AgentContext agent, VaultDocument snapshot, KnowledgeDocument? document, CancellationToken ct)
    {
        await audit.WriteAsync(Event(agent.Id, "brain.knowledge.read", snapshot.Path), ct);
        return new(document?.Id, snapshot.Path, chunker.Chunk(snapshot.Path, snapshot.Content).Title,
            snapshot.Content, snapshot.RevisionToken, snapshot.ContentHash, snapshot.ModifiedAt,
            document?.ContentHash == snapshot.ContentHash && document.ChunkerVersion == chunker.Version && MatchesModel(document));
    }

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(AgentContext agent, KnowledgeSearchRequest request, CancellationToken ct = default)
    {
        agent.Require(Permissions.KnowledgeRead);
        Validation.Text(request.Query, "Query", 4000);
        Validation.Require(request.Limit is >= 1 and <= 100, "Limit must be between 1 and 100.");
        if (request.PathPrefix is not null)
            Validation.Require(request.PathPrefix.Length is > 0 and <= 1000 &&
                request.PathPrefix.Split('/').All(s => s.Length > 0 && s is not "." and not ".." && !s.StartsWith('.')) &&
                !request.PathPrefix.Any(c => char.IsControl(c) || "\\:<>\"|?*".Contains(c)), "Invalid path prefix. Use vault-relative forward-slash paths without a trailing slash.");
        var results = await index.SearchAsync(request, await EmbedAsync(request.Query, ct), embeddings.Model, ct);
        // Never return a cached section from a deleted/linked/changed canonical file. Until the next scan, omit stale hits.
        var snapshots = new Dictionary<string, string?>(StringComparer.Ordinal);
        var current = new List<KnowledgeSearchResult>();
        foreach (var result in results)
        {
            if (!snapshots.TryGetValue(result.Source, out var revision))
            {
                try { revision = (await vault.ReadAsync(result.Source, ct)).RevisionToken; }
                catch (BrainException e) when (e.StatusCode is 404 or 400 or 413 or 422) { revision = null; }
                snapshots[result.Source] = revision;
            }
            if (revision == result.Metadata.RevisionToken) current.Add(result);
        }
        await audit.WriteAsync(Event(agent.Id, "brain.knowledge.searched", "knowledge"), ct);
        return current;
    }

    public async Task<KnowledgeScanResult> ScanAsync(AgentContext agent, CancellationToken ct = default)
    {
        agent.Require(Permissions.KnowledgeAdmin);
        return await ScanCoreAsync(agent.Id, ct);
    }

    public async Task<KnowledgeScanResult> ScanCoreAsync(string actor, CancellationToken ct = default)
    {
        await using var lease = await index.TryAcquireScanAsync(ct);
        if (lease is null) return new(0, 0, 0, [], true);
        var paths = vault.ListMarkdownPaths(ct); // Incomplete enumeration aborts; never prune on a partial directory listing.
        var previous = await index.ListAsync(ct);
        var byPath = previous.ToDictionary(d => d.Path, StringComparer.Ordinal);
        var seen = paths.ToHashSet(StringComparer.Ordinal);
        var failures = new List<KnowledgeScanFailure>();
        var indexed = 0;
        var unchanged = 0;
        var removed = 0;
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var source = await vault.ReadAsync(path, ct);
                byPath.TryGetValue(path, out var document);
                if (document is not null && document.ContentHash == source.ContentHash &&
                    document.ChunkerVersion == chunker.Version && MatchesModel(document) &&
                    document.GraphVersion == 1)
                { unchanged++; continue; }
                var parsed = chunker.Chunk(path, source.Content);
                var chunks = new List<EmbeddedKnowledgeChunk>();
                foreach (var chunk in parsed.Chunks)
                    chunks.Add(new(chunk, await EmbedAsync($"{parsed.Title}\n{chunk.Heading}\n{chunk.Content}", ct)));
                // A human may edit while embeddings are generated. Keep the old index and retry next scan.
                if ((await vault.ReadAsync(path, ct)).RevisionToken != source.RevisionToken)
                { failures.Add(new(path, "Document changed during indexing; retry on next scan.")); continue; }
                document = new KnowledgeDocument
                {
                    Id = document?.Id ?? Guid.NewGuid(), Path = source.Path, Title = parsed.Title,
                    ContentHash = source.ContentHash, RevisionToken = source.RevisionToken, ModifiedAt = source.ModifiedAt,
                    IndexedAt = time.GetUtcNow(), EmbeddingProvider = embeddings.Model.Provider,
                    EmbeddingModel = embeddings.Model.Model, EmbeddingDimensions = embeddings.Model.Dimensions, ChunkerVersion = chunker.Version,
                    GraphReferences = graphReferences.Extract(source.Content), GraphVersion = 1
                };
                await index.ReplaceAsync(document, chunks, Event(actor, "brain.knowledge.indexed", path), ct);
                indexed++;
            }
            catch (BrainException e) when (e.StatusCode is 400 or 404 or 413 or 422)
            { failures.Add(new(path, e.Message)); }
        }
        foreach (var deleted in previous.Where(d => !seen.Contains(d.Path)))
        {
            await index.RemoveAsync(deleted.Id, Event(actor, "brain.knowledge.removed", deleted.Path), ct);
            removed++;
        }
        await audit.WriteAsync(new AuditEvent
        {
            AgentId = actor, Action = "brain.knowledge.scanned", Resource = "knowledge", Timestamp = time.GetUtcNow(),
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { indexed, unchanged, removed, failed = failures.Count })
        }, ct);
        return new(indexed, unchanged, removed, failures);
    }

    private bool MatchesModel(KnowledgeDocument d) => d.EmbeddingProvider == embeddings.Model.Provider &&
        d.EmbeddingModel == embeddings.Model.Model && d.EmbeddingDimensions == embeddings.Model.Dimensions;
    private async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var vector = await embeddings.EmbedAsync(text, ct);
        if (vector.Length != embeddings.Model.Dimensions || vector.Any(v => !float.IsFinite(v)) || !vector.Any(v => v != 0))
            throw new InvalidOperationException("Embedding provider returned an invalid vector.");
        return vector;
    }
    private AuditEvent Event(string actor, string action, string path) => new()
    { AgentId = actor, Action = action, Resource = path, Timestamp = time.GetUtcNow() };
}
