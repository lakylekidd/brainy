namespace Brain.Domain;

public enum MemoryType { Observation, Fact, Decision, TaskContext, Incident, Preference, Discovery }
public enum MemoryVisibility { Private, Team, Organization }
public enum PromotionStatus { None, Candidate, Approved, Rejected, Promoted }

public sealed class AgentMemory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AgentId { get; set; } = "";
    public MemoryType MemoryType { get; set; }
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public double Importance { get; set; }
    public double Confidence { get; set; }
    public MemoryVisibility Visibility { get; set; }
    public string? TeamId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Source { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public long ReferenceCount { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public PromotionStatus PromotionStatus { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AgentId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string Result { get; set; } = "success";
    public string MetadataJson { get; set; } = "{}";
}
