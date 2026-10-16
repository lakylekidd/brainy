using Brain.Domain;

namespace Brain.Application;

public sealed record ProposeKnowledgeChangeRequest(string Path, KnowledgeOperation Operation, string Content,
    string Reason, string? RevisionToken = null, string? Section = null);
public sealed record ReviewKnowledgeProposalRequest(int Version, string Reason);
public sealed record KnowledgeProposalView(Guid Id, string AgentId, string Path, KnowledgeOperation Operation,
    string Content, string Reason, string? RevisionToken, string? Section, KnowledgeProposalStatus Status,
    int Version, string? ReviewerId, string? ReviewReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? AppliedAt, string? GitCommit, string? LastError, int? MetadataSchemaVersion = null,
    string? EnrichmentProvider = null, string? EnrichmentModel = null, double? EnrichmentConfidence = null, string? GeneratedBy = null)
{
    public string Type => Operation == KnowledgeOperation.MetadataEnrichment ? "metadata_enrichment" : "knowledge_change";
    public static KnowledgeProposalView From(KnowledgeWriteProposal p) => new(p.Id, p.AgentId, p.Path, p.Operation,
        p.Content, p.Reason, p.RevisionToken, p.Section, p.Status, p.Version, p.ReviewerId, p.ReviewReason,
        p.CreatedAt, p.UpdatedAt, p.AppliedAt, p.GitCommit, p.LastError, p.MetadataSchemaVersion,
        p.EnrichmentProvider, p.EnrichmentModel, p.EnrichmentConfidence, p.GeneratedBy);
}
public sealed record KnowledgeProposalPreview(string Path, string? CurrentRevisionToken, string? Before,
    string? After, bool RevisionMatches, string? ConflictReason);
public sealed record PreparedKnowledgeWrite(string Branch, string ParentCommit, string Commit, string Blob,
    string? BaseContentHash, string ResultContentHash, string ResultContent, bool HasUtf8Bom);
public sealed record KnowledgeWriterResult(int Applied, int Conflicts, int Rejected, int Deferred, bool AlreadyRunning = false);

public interface IKnowledgeProposalStore
{
    Task CreateAsync(KnowledgeWriteProposal proposal, AuditEvent audit, CancellationToken ct);
    Task<KnowledgeWriteProposal?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeWriteProposal>> ListAsync(string? agentId, KnowledgeProposalStatus? status, int limit, CancellationToken ct, bool? metadataEnrichment = null);
    Task SaveAsync(KnowledgeWriteProposal proposal, AuditEvent audit, CancellationToken ct);
    Task<IAsyncDisposable?> TryAcquireWriterAsync(CancellationToken ct);
    Task<IReadOnlyList<KnowledgeWriteProposal>> QueuedAsync(int limit, CancellationToken ct);
}

// Implemented only by the worker's infrastructure. HTTP services only submit/review proposals.
public interface ICanonicalKnowledgeWriter
{
    Task<PreparedKnowledgeWrite> PrepareAsync(KnowledgeWriteProposal proposal, CancellationToken ct);
    Task ApplyAsync(KnowledgeWriteProposal preparedProposal, CancellationToken ct);
}
