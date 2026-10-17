namespace Brain.Domain;

public sealed class Agent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? TeamId { get; set; }
    public bool Enabled { get; set; } = true;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Role> Roles { get; set; } = [];
    public List<Permission> Permissions { get; set; } = [];
}

public sealed class Role
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Permission> Permissions { get; set; } = [];
}

public sealed class Permission
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
}

public static class Permissions
{
    public const string MemoryRead = "memory.read";
    public const string MemoryWrite = "memory.write";
    public const string MemoryDelete = "memory.delete";
    public const string MemoryShare = "memory.share";
    public const string AgentsRead = "agents.read";
    public const string AgentsAdmin = "agents.admin";
    public const string AuditRead = "audit.read";
    public const string KnowledgeRead = "knowledge.read";
    public const string KnowledgeAdmin = "knowledge.admin";
    public const string KnowledgePropose = "knowledge.propose";
    public const string RegistryRead = "registry.read";
    public const string RegistryAdmin = "registry.admin";
    public const string ActivityRead = "activity.read";
    public const string ActivityLive = "activity.live";
    public const string TracesRead = "traces.read";
    public const string TracesReadAll = "traces.read.all";
    public static readonly string[] All = [MemoryRead, MemoryWrite, MemoryDelete, MemoryShare, AgentsRead, AgentsAdmin, AuditRead,
        KnowledgeRead, KnowledgeAdmin, KnowledgePropose, RegistryRead, RegistryAdmin,
        ActivityRead, ActivityLive, TracesRead, TracesReadAll,
        "infrastructure.read", "infrastructure.write", "deployment.read", "deployment.write", "deployment.staging",
        "deployment.production", "secrets.dev.read", "secrets.production.read"];
}

public sealed record AgentContext(string Id, string? TeamId, IReadOnlySet<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission);
    public void Require(string permission)
    {
        if (!Has(permission)) throw new BrainException(403, "The agent lacks the required permission.");
    }
}

public class BrainException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ProposalConcurrencyException() : BrainException(409, "The proposal changed concurrently.");
