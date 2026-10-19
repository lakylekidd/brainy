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
    public const string DevicesRead = "devices.read";
    public const string DevicesRegister = "devices.register";
    public const string DevicesManage = "devices.manage";
    public const string DevicesRevoke = "devices.revoke";
    public const string HarnessesConnect = "harnesses.connect";
    public const string HarnessesManage = "harnesses.manage";
    public const string RuntimeRead = "runtime.read";
    public const string RuntimeProvision = "runtime.provision";
    public const string SkillsSync = "skills.sync";
    public static readonly string[] All = [MemoryRead, MemoryWrite, MemoryDelete, MemoryShare, AgentsRead, AgentsAdmin, AuditRead,
        KnowledgeRead, KnowledgeAdmin, KnowledgePropose, RegistryRead, RegistryAdmin,
        ActivityRead, ActivityLive, TracesRead, TracesReadAll, DevicesRead, DevicesRegister, DevicesManage,
        DevicesRevoke, HarnessesConnect, HarnessesManage, RuntimeRead, RuntimeProvision, SkillsSync,
        "infrastructure.read", "infrastructure.write", "deployment.read", "deployment.write", "deployment.staging",
        "deployment.production", "secrets.dev.read", "secrets.production.read"];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MemoryRead] = "Read memories allowed by their private, team, or organization visibility.",
        [MemoryWrite] = "Create memories. Sharing them with a team or organization also requires memory.share.",
        [MemoryDelete] = "Delete memories owned by this agent.",
        [MemoryShare] = "Create team or organization memories, subject to the normal visibility rules.",
        [AgentsRead] = "View the agent directory, agent details, roles, and permission catalog.",
        [AgentsAdmin] = "Register agents and manage other agents' roles, direct grants, and enabled state.",
        [AuditRead] = "Read the Brain audit event history.",
        [KnowledgeRead] = "Search and read documents in the shared knowledge vault.",
        [KnowledgeAdmin] = "Review and decide knowledge change proposals, including proposals from other agents.",
        [KnowledgePropose] = "Submit proposed knowledge or metadata changes for review; does not write canonical files directly.",
        [RegistryRead] = "Discover and read skills and tools whose required permissions are granted.",
        [RegistryAdmin] = "Run registry scans that refresh the searchable skills and tools index.",
        [ActivityRead] = "Use the activity stream; activity.live is also required to receive live events.",
        [ActivityLive] = "Subscribe to live activity within the caller's trace visibility scope.",
        [TracesRead] = "Inspect this agent's retained trace summaries and events.",
        [TracesReadAll] = "Inspect retained traces and live activity across all agents.",
        [DevicesRead] = "View devices owned by this agent and their reported status.",
        [DevicesRegister] = "Enroll a device bound to this agent identity.",
        [DevicesManage] = "Manage this agent's devices, including viewing, renaming, rotating credentials, and reporting status.",
        [DevicesRevoke] = "Revoke another agent's device; managing another agent's device also requires agents.admin.",
        [HarnessesConnect] = "Connect supported local harnesses to Brain's MCP service.",
        [HarnessesManage] = "Manage agent harness connection settings.",
        [RuntimeRead] = "Inspect runtime and dependency reports for this agent's devices.",
        [RuntimeProvision] = "Request or approve an eligible, operator-approved runtime package installation.",
        [SkillsSync] = "Report skill synchronization status for the device bound to this agent.",
        ["infrastructure.read"] = "Meet a skill or tool requirement for infrastructure access; the grant alone does not execute actions.",
        ["infrastructure.write"] = "Meet a skill or tool requirement for infrastructure changes; the grant alone does not execute actions.",
        ["deployment.read"] = "Meet a skill or tool requirement for deployment reads; the grant alone does not deploy anything.",
        ["deployment.write"] = "Meet a skill or tool requirement for deployment changes; the grant alone does not deploy anything.",
        ["deployment.staging"] = "Meet a skill or tool requirement for staging deployments; the grant alone does not deploy anything.",
        ["deployment.production"] = "Meet a skill or tool requirement for production deployments; the grant alone does not deploy anything.",
        ["secrets.dev.read"] = "Meet a skill or tool requirement to reference development secrets; secret values remain outside Brain.",
        ["secrets.production.read"] = "Meet a skill or tool requirement to reference production secrets; secret values remain outside Brain."
    };
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
