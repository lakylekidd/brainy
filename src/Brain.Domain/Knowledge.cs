namespace Brain.Domain;

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string RevisionToken { get; set; } = "";
    public DateTimeOffset ModifiedAt { get; set; }
    public DateTimeOffset IndexedAt { get; set; }
    public string EmbeddingProvider { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public int EmbeddingDimensions { get; set; }
    public string ChunkerVersion { get; set; } = "";
}
