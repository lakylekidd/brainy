using System.ComponentModel;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Brain.Mcp;

public sealed record MemorySearchResponse(IReadOnlyList<MemorySearchResult> Memories);
public sealed record SkillSearchResponse(IReadOnlyList<RegistryResult> Skills);

[McpServerToolType]
public sealed class BrainTools(ICurrentAgent current, MemoryService memories, KnowledgeService knowledge,
    KnowledgeProposalService proposals, RegistryService registry, SearchService search, IAuditLog audit, TimeProvider time)
{
    [McpServerTool(Name = "brain_search", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search permitted knowledge, visible memories, skills and tools. Unavailable optional sources are reported in failures. Scores are per source; this does not execute external tools.")]
    public Task<BrainSearchResult> Search(string query, int limit = 10, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_search", () => search.SearchAsync(current.Context, new(query, limit), cancellationToken), cancellationToken);

    [McpServerTool(Name = "brain_remember", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Store a natural-language memory for the authenticated agent. Visibility defaults to private; team/organization sharing needs memory.share. Never store secret values.")]
    public Task<MemoryView> Remember(string subject, string content, MemoryType memoryType = MemoryType.Observation,
        MemoryVisibility visibility = MemoryVisibility.Private, double importance = 0.5, double confidence = 0.5,
        DateTimeOffset? expiresAt = null, string? source = null, JsonElement? metadata = null, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_remember", () => memories.RememberAsync(current.Context,
            new(memoryType, subject, content, visibility, importance, confidence, expiresAt, source, metadata), cancellationToken), cancellationToken);

    [McpServerTool(Name = "brain_search_memories", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search private, team and organization memories visible to the authenticated agent. Optional filters never grant additional access.")]
    public async Task<MemorySearchResponse> SearchMemories(string query, MemoryType? memoryType = null, string? subject = null,
        double? minimumImportance = null, double? minimumConfidence = null, int limit = 10, CancellationToken cancellationToken = default) =>
        new(await InvokeAsync("brain_search_memories", () => memories.SearchAsync(current.Context,
            new(query, MemoryType: memoryType, Subject: subject, MinimumImportance: minimumImportance,
                MinimumConfidence: minimumConfidence, Limit: limit), cancellationToken), cancellationToken));

    [McpServerTool(Name = "brain_read_document", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read a canonical Markdown document by vault-relative forward-slash path. Returns its content and revisionToken for a later proposal. Arbitrary filesystem access is prohibited.")]
    public Task<KnowledgeDocumentView> ReadDocument(string path, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_read_document", () => knowledge.ReadPathAsync(current.Context, path, cancellationToken), cancellationToken);

    [McpServerTool(Name = "brain_propose_knowledge_change", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Queue a pending knowledge change for review. Supported caller operations are append, replace_section and create_document; metadata_enrichment is scanner-only. append and replace_section require the document's revisionToken; create_document requires a new path. This never approves or directly writes canonical files.")]
    public Task<KnowledgeProposalView> ProposeKnowledgeChange(string path, KnowledgeOperation operation, string content, string reason,
        string? revisionToken = null, string? section = null, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_propose_knowledge_change", () => proposals.ProposeAsync(current.Context,
            new(path, operation, content, reason, revisionToken, section), cancellationToken), cancellationToken);

    [McpServerTool(Name = "brain_find_skills", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Find indexed skills by similarity and optional exact tags. Only skills with all required permissions and authorized, current tool dependencies are returned.")]
    public async Task<SkillSearchResponse> FindSkills(string query, string[]? tags = null, int limit = 10, CancellationToken cancellationToken = default) =>
        new(await InvokeAsync("brain_find_skills", () => registry.SearchAsync(current.Context, RegistryKind.Skill,
            new(query, tags, limit), cancellationToken), cancellationToken));

    [McpServerTool(Name = "brain_get_skill", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read an allowed skill's metadata and canonical instructions by id. All skill and tool dependency permissions are checked on this request. Instructions do not grant authority to execute tools.")]
    public Task<SkillView> GetSkill(string skillId, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_get_skill", () => registry.GetSkillAsync(current.Context, skillId, cancellationToken), cancellationToken);

    [McpServerTool(Name = "brain_find_capabilities", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Discover allowed skills and related allowed tools for a task. Every required dependency must be authorized. Tool dependencies can exceed limit; discovery never invokes an external system.")]
    public Task<CapabilitySearchResult> FindCapabilities(string query, string[]? tags = null, int limit = 10, CancellationToken cancellationToken = default) =>
        InvokeAsync("brain_find_capabilities", () => registry.FindCapabilitiesAsync(current.Context,
            new(query, tags, limit), cancellationToken), cancellationToken);

    private async Task<T> InvokeAsync<T>(string tool, Func<Task<T>> invoke, CancellationToken ct)
    {
        try { return await invoke(); }
        catch (BrainException e)
        {
            if (e.StatusCode == 403)
                await audit.WriteAsync(new AuditEvent { AgentId = current.Context.Id, Action = "brain.authorization.denied",
                    Resource = tool, Result = "denied", Timestamp = time.GetUtcNow() }, ct);
            // Only controlled application errors are model-visible. The SDK hides unexpected exception details.
            throw new McpException($"Brain {e.StatusCode}: {e.Message}");
        }
    }
}
