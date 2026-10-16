using Brain.Domain;

namespace Brain.Application;

public sealed class KnowledgeWriterService(IKnowledgeProposalStore store, ICanonicalKnowledgeWriter writer,
    IAgentDirectory agents, TimeProvider time)
{
    public async Task<KnowledgeWriterResult> ProcessAsync(int limit = 25, CancellationToken ct = default)
    {
        Validation.Require(limit is >= 1 and <= 100, "Writer batch limit must be between 1 and 100.");
        await using var lease = await store.TryAcquireWriterAsync(ct);
        if (lease is null) return new(0, 0, 0, 0, true);
        var applied = 0; var conflicts = 0; var rejected = 0; var deferred = 0;
        foreach (var proposal in await store.QueuedAsync(limit, ct))
        {
            try
            {
                if (proposal.Status == KnowledgeProposalStatus.Approved)
                {
                    var author = await agents.ResolveAsync(proposal.AgentId, ct);
                    var reviewer = proposal.ReviewerId is null ? null : await agents.ResolveAsync(proposal.ReviewerId, ct);
                    if (author is null || !author.Has(Permissions.KnowledgePropose) || !author.Has(Permissions.KnowledgeRead) ||
                        reviewer is null || !reviewer.Has(Permissions.KnowledgeAdmin))
                        throw new BrainException(400, "Proposer or reviewer no longer has the required access.");
                    var prepared = await writer.PrepareAsync(proposal, ct);
                    proposal.GitBranch = prepared.Branch; proposal.ParentCommit = prepared.ParentCommit;
                    proposal.GitCommit = prepared.Commit; proposal.GitBlob = prepared.Blob;
                    proposal.BaseContentHash = prepared.BaseContentHash; proposal.ResultContentHash = prepared.ResultContentHash;
                    proposal.ResultContent = prepared.ResultContent; proposal.ResultHasUtf8Bom = prepared.HasUtf8Bom;
                    proposal.Status = KnowledgeProposalStatus.Validating; proposal.UpdatedAt = time.GetUtcNow(); proposal.LastError = null;
                    await store.SaveAsync(proposal, Event("brain.knowledge.prepared", proposal), ct);
                }
                await writer.ApplyAsync(proposal, ct);
                proposal.Status = KnowledgeProposalStatus.Applied; proposal.AppliedAt = time.GetUtcNow(); proposal.UpdatedAt = time.GetUtcNow();
                proposal.LastError = null; proposal.ResultContent = null; // Git retains the canonical history; clear the recovery body.
                await store.SaveAsync(proposal, Event(proposal.Operation == KnowledgeOperation.MetadataEnrichment
                    ? "brain.metadata.enrichment.applied" : "brain.knowledge.applied", proposal), ct);
                applied++;
            }
            catch (ProposalConcurrencyException) { throw; } // Preserve the concurrent database winner; never turn it into a content conflict.
            catch (BrainException e) when (e.StatusCode is 400 or 409 or 413 or 422)
            {
                proposal.Status = e.StatusCode == 409 ? KnowledgeProposalStatus.Conflict : KnowledgeProposalStatus.Rejected;
                proposal.LastError = e.Message; proposal.UpdatedAt = time.GetUtcNow();
                await store.SaveAsync(proposal, Event(proposal.Status == KnowledgeProposalStatus.Conflict ? "brain.knowledge.conflict" : "brain.knowledge.rejected", proposal), ct);
                if (proposal.Status == KnowledgeProposalStatus.Conflict) conflicts++; else rejected++;
            }
            catch (BrainException e) when (e.StatusCode == 503)
            {
                proposal.LastError = e.Message; proposal.UpdatedAt = time.GetUtcNow();
                await store.SaveAsync(proposal, Event("brain.knowledge.deferred", proposal), ct);
                deferred++;
                break; // Resolve repository/recovery issues before publishing another proposal.
            }
        }
        return new(applied, conflicts, rejected, deferred);
    }
    private AuditEvent Event(string action, KnowledgeWriteProposal p) => new()
    { AgentId = p.AgentId, Action = action, Resource = p.Id.ToString(), Timestamp = time.GetUtcNow() };
}
