using System.Text.Json;
using Brain.Domain;

namespace Brain.Application;

public sealed record EmbeddingModel(string Provider, string Model, int Dimensions);
public interface IEmbeddingProvider
{
    EmbeddingModel Model { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}

public sealed record RememberRequest(
    MemoryType MemoryType, string Subject, string Content,
    MemoryVisibility Visibility = MemoryVisibility.Private,
    double Importance = 0.5, double Confidence = 0.5,
    DateTimeOffset? ExpiresAt = null, string? Source = null, JsonElement? Metadata = null);

public sealed record MemorySearchRequest(
    string Query, string? AgentId = null, MemoryType? MemoryType = null,
    string? Subject = null, double? MinimumImportance = null,
    double? MinimumConfidence = null, int Limit = 10);

public sealed record MemoryView(
    Guid Id, string AgentId, MemoryType MemoryType, string Subject, string Content,
    MemoryVisibility Visibility, string? TeamId, double Importance, double Confidence,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ExpiresAt,
    string? Source, JsonElement Metadata, long ReferenceCount,
    DateTimeOffset? LastAccessedAt, PromotionStatus PromotionStatus)
{
    public static MemoryView From(AgentMemory m) => new(m.Id, m.AgentId, m.MemoryType, m.Subject,
        m.Content, m.Visibility, m.TeamId, m.Importance, m.Confidence, m.CreatedAt, m.UpdatedAt,
        m.ExpiresAt, m.Source, JsonSerializer.Deserialize<JsonElement>(m.MetadataJson),
        m.ReferenceCount, m.LastAccessedAt, m.PromotionStatus);
}

public sealed record MemorySearchResult(string Type, Guid Id, string Title, string Content,
    double Score, string? Source, MemoryView Metadata);
public sealed record ScoredMemory(AgentMemory Memory, double Score);

public interface IMemoryStore
{
    Task CreateAsync(AgentMemory memory, float[] embedding, EmbeddingModel model, AuditEvent audit, CancellationToken ct);
    Task<AgentMemory?> GetVisibleAsync(AgentContext agent, Guid id, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<ScoredMemory>> SearchAsync(AgentContext agent, MemorySearchRequest request,
        float[] embedding, EmbeddingModel model, DateTimeOffset now, CancellationToken ct);
    Task<bool> DeleteOwnedAsync(string agentId, Guid id, AuditEvent audit, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed record AgentView(string Id, string Name, string Description, string? TeamId,
    bool Enabled, string[] Roles, string[] Permissions, JsonElement Metadata,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string[] DirectPermissions);
public sealed record RoleView(string Id, string Description, string[] Permissions);
public sealed record PermissionView(string Id, string Description);
public sealed record CreateAgentRequest(string Id, string Name, string Description = "", string? TeamId = null,
    string[]? Roles = null, string[]? Permissions = null, JsonElement? Metadata = null);
public sealed record UpdateAccessRequest(string[] Roles, string[] Permissions, bool Enabled = true);

public interface IAgentDirectory
{
    Task<AgentContext?> ResolveAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<AgentView>> ListAsync(AgentContext actor, CancellationToken ct);
    Task<AgentView> GetAsync(AgentContext actor, string id, CancellationToken ct);
    Task<IReadOnlyList<RoleView>> ListRolesAsync(AgentContext actor, CancellationToken ct);
    Task<IReadOnlyList<PermissionView>> ListPermissionsAsync(AgentContext actor, CancellationToken ct);
    Task<AgentView> CreateAsync(AgentContext actor, CreateAgentRequest request, CancellationToken ct);
    Task<AgentView> UpdateAccessAsync(AgentContext actor, string id, UpdateAccessRequest request, CancellationToken ct);
}

public interface IAuditLog
{
    Task WriteAsync(AuditEvent audit, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> ListAsync(AgentContext actor, int limit, CancellationToken ct);
}
