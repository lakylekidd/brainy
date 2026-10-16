using Brain.Domain;

namespace Brain.Application;

public sealed record VaultDocument(string Path, string Content, string ContentHash, string RevisionToken, DateTimeOffset ModifiedAt, bool HasUtf8Bom = false);
public interface IKnowledgeVault
{
    IReadOnlyList<string> ListMarkdownPaths(CancellationToken ct);
    Task<VaultDocument> ReadAsync(string path, CancellationToken ct);
}

public sealed record MarkdownChunk(int Ordinal, string Heading, string Content, int StartLine, int EndLine);
public sealed record ChunkedMarkdown(string Title, IReadOnlyList<MarkdownChunk> Chunks);
public interface IMarkdownChunker
{
    string Version { get; }
    ChunkedMarkdown Chunk(string path, string content);
}
public sealed record EmbeddedKnowledgeChunk(MarkdownChunk Chunk, float[] Embedding);
public sealed record KnowledgeSearchRequest(string Query, string? PathPrefix = null, int Limit = 10);
public sealed record KnowledgeSearchResult(string Type, Guid Id, string Title, string Content, double Score,
    string Source, KnowledgeResultMetadata Metadata);
public sealed record KnowledgeResultMetadata(Guid DocumentId, string Heading, int StartLine, int EndLine,
    string RevisionToken, string ContentHash, DateTimeOffset IndexedAt);
public sealed record KnowledgeDocumentView(Guid? Id, string Path, string Title, string Content, string RevisionToken,
    string ContentHash, DateTimeOffset ModifiedAt, bool IndexCurrent);
public sealed record KnowledgeScanFailure(string Path, string Reason);
public sealed record KnowledgeScanResult(int Indexed, int Unchanged, int Removed,
    IReadOnlyList<KnowledgeScanFailure> Failures, bool AlreadyRunning = false);

public interface IKnowledgeIndex
{
    Task<IAsyncDisposable?> TryAcquireScanAsync(CancellationToken ct);
    Task<IReadOnlyList<KnowledgeDocument>> ListAsync(CancellationToken ct);
    Task<KnowledgeDocument?> FindAsync(Guid id, CancellationToken ct);
    Task<KnowledgeDocument?> FindByPathAsync(string path, CancellationToken ct);
    Task ReplaceAsync(KnowledgeDocument document, IReadOnlyList<EmbeddedKnowledgeChunk> chunks, AuditEvent audit, CancellationToken ct);
    Task RemoveAsync(Guid id, AuditEvent audit, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(KnowledgeSearchRequest request, float[] vector, EmbeddingModel model, CancellationToken ct);
}
