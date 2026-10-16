using System.Text.Json;
using Brain.Domain;

namespace Brain.Application;

public sealed class MetadataService(IKnowledgeVault vault, IMetadataDocumentProcessor documents, IKnowledgeProvenance provenance,
    IMetadataEnrichmentProvider provider, IMetadataScanStore store, KnowledgeProposalService proposals,
    IAgentDirectory agents, IAuditLog audit, MetadataOptions options, TimeProvider time)
{
    public async Task<IReadOnlyList<MetadataIssueView>> IssuesAsync(AgentContext actor, int limit = 100, CancellationToken ct = default)
    {
        actor.Require(Permissions.KnowledgeRead); actor.Require(Permissions.KnowledgeAdmin);
        Validation.Require(limit is >= 1 and <= 1000, "Limit must be between 1 and 1000.");
        return (await store.IssuesAsync(limit, ct)).Select(s => new MetadataIssueView(s.Path, s.RevisionToken, s.SchemaVersion,
            JsonSerializer.Deserialize<MetadataIssue[]>(s.IssuesJson, MetadataJson.Options)!, s.ProposalId, s.CheckedAt,
            s.LastEnrichedAt, s.Provider, s.Model, s.LastError)).ToArray();
    }

    public Task<MetadataScanResult> ScanAsync(AgentContext actor, CancellationToken ct = default)
    {
        actor.Require(Permissions.KnowledgeRead); actor.Require(Permissions.KnowledgeAdmin); actor.Require(Permissions.KnowledgePropose);
        return ScanCoreAsync(actor.Id, ct);
    }

    public async Task<MetadataScanResult> ScanCoreAsync(string agentId, CancellationToken ct = default)
    {
        MetadataJson.ValidateOptions(options);
        var actor = await agents.ResolveAsync(agentId, ct) ?? throw new BrainException(403, "Metadata scanner identity is missing or disabled.");
        actor.Require(Permissions.KnowledgeRead); actor.Require(Permissions.KnowledgePropose);
        await using var lease = await store.TryAcquireAsync(ct);
        if (lease is null) return new(0, 0, 0, 0, 0, 0, true);
        await audit.WriteAsync(Event(actor.Id, "brain.metadata.scan.started", "knowledge"), ct);
        var paths = vault.ListMarkdownPaths(ct); // Incomplete enumeration aborts; no state pruning on a partial listing.
        var info = options.SemanticEnabled ? provider.Info : new MetadataProviderInfo("deterministic", "metadata-v1", "metadata-v1");
        Validation.Require(info.Provider.Length <= 200 && info.Model.Length <= 200, "Metadata provider identity is too long.");
        var policyHash = MetadataJson.Hash(documents.SchemaHash + "\n" + options.SemanticEnabled + "\n" + info.ConfigurationKey + "\n" + info.Provider + "\n" + info.Model);
        var inspected = 0; var skipped = 0; var issues = 0; var proposed = 0; var failed = 0;
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            MetadataScanState? state = null;
            try
            {
                var source = await vault.ReadAsync(path, ct);
                state = await store.FindAsync(path, ct);
                if (state is not null && state.ContentHash == source.ContentHash && state.PolicyHash == policyHash && state.LastError is null)
                { skipped++; continue; }
                var previous = state;
                state = new MetadataScanState
                {
                    Path = path, ContentHash = source.ContentHash, RevisionToken = source.RevisionToken, SchemaVersion = options.SchemaVersion,
                    PolicyHash = policyHash, CheckedAt = time.GetUtcNow(), LastEnrichedAt = previous?.LastEnrichedAt,
                    Provider = previous?.Provider, Model = previous?.Model
                };
                var inspection = documents.Inspect(source);
                inspected++;
                issues += inspection.Issues.Count > 0 ? 1 : 0;
                state.IssuesJson = JsonSerializer.Serialize(inspection.Issues, MetadataJson.Options);
                var events = new List<AuditEvent>();
                if (inspection.Issues.Count > 0) events.Add(Event(actor.Id, "brain.metadata.issue.detected", path));
                KnowledgeWriteProposal? proposal = null;
                if (inspection.Issues.Count > 0 && inspection.CanEdit)
                {
                    SemanticMetadata? semantic = null;
                    if (options.SemanticEnabled)
                    {
                        var length = Math.Min(inspection.Body.Length, 12000);
                        if (length > 0 && char.IsHighSurrogate(inspection.Body[length - 1])) length--;
                        semantic = MetadataJson.ParseSemantic(await provider.GenerateJsonAsync(
                            new(path, inspection.Body[..length], inspection.ValidFields, options.DocumentTypes), ct), options);
                    }
                    var patch = documents.BuildPatch(source, await provenance.ReadAsync(path, ct), semantic);
                    // Re-resolve author grants after potentially slow provider I/O, before creating the pending proposal.
                    actor = await agents.ResolveAsync(agentId, ct) ?? throw new BrainException(403, "Metadata scanner identity was disabled.");
                    proposal = await proposals.BuildMetadataAsync(actor, source, patch, info, semantic?.Confidence, ct);
                    state.ProposalId = proposal.Id; state.LastEnrichedAt = time.GetUtcNow(); state.Provider = info.Provider; state.Model = info.Model;
                    events.Add(Event(actor.Id, "brain.metadata.enrichment.generated", path));
                    events.Add(Event(actor.Id, "brain.metadata.proposal.created", proposal.Id.ToString()));
                }
                await store.SaveAsync(state, proposal, events, ct);
                if (proposal is not null) proposed++;
            }
            catch (BrainException e) when (e.StatusCode is 400 or 404 or 409 or 413 or 422 or 503)
            {
                failed++;
                if (state is not null)
                {
                    state.LastError = e.Message;
                    await store.SaveAsync(state, null, [Event(actor.Id, "brain.metadata.enrichment.failed", path, "failed")], ct);
                }
                else await audit.WriteAsync(Event(actor.Id, "brain.metadata.enrichment.failed", path, "failed"), ct);
            }
        }
        var removed = await store.RemoveMissingAsync(paths, ct);
        await audit.WriteAsync(Event(actor.Id, "brain.metadata.scan.completed", "knowledge"), ct);
        return new(inspected, skipped, issues, proposed, failed, removed);
    }
    private AuditEvent Event(string actor, string action, string resource, string result = "success") => new()
    { AgentId = actor, Action = action, Resource = resource, Result = result, Timestamp = time.GetUtcNow() };
}
