namespace Brain.Domain;

public enum KnowledgeOperation { Append, ReplaceSection, CreateDocument, MetadataEnrichment }
public enum KnowledgeProposalStatus { Pending, Validating, Approved, Rejected, Conflict, Applied }

public sealed class KnowledgeWriteProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AgentId { get; set; } = "";
    public string Path { get; set; } = "";
    public string? RevisionToken { get; set; }
    public KnowledgeOperation Operation { get; set; }
    public string Content { get; set; } = "";
    public string? Section { get; set; }
    public string Reason { get; set; } = "";
    public KnowledgeProposalStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ReviewerId { get; set; }
    public string? ReviewReason { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public string? LastError { get; set; }
    public int Version { get; set; }
    public int? MetadataSchemaVersion { get; set; }
    public string? EnrichmentProvider { get; set; }
    public string? EnrichmentModel { get; set; }
    public double? EnrichmentConfidence { get; set; }
    public string? GeneratedBy { get; set; }

    // Durable intent, saved before publishing a Git ref or modifying canonical Markdown.
    public string? GitBranch { get; set; }
    public string? ParentCommit { get; set; }
    public string? GitCommit { get; set; }
    public string? GitBlob { get; set; }
    public string? BaseContentHash { get; set; }
    public string? ResultContentHash { get; set; }
    public string? ResultContent { get; set; }
    public bool ResultHasUtf8Bom { get; set; }
}
