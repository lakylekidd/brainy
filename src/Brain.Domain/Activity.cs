namespace Brain.Domain;

public enum ActivityStatus { Started, Running, Succeeded, Failed, Cancelled }

// Ownership is independent of a caller-supplied trace ID.
public sealed class ActivityTrace
{
    public string TraceId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string RootSpanId { get; set; } = "";
    public string RootAction { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ActivityStatus Status { get; set; }
    public bool HasFailures { get; set; }
    public bool HasCancellations { get; set; }
    public int ActiveRoots { get; set; }
    public double? DurationMs { get; set; }
}

public sealed class RetainedActivityEvent
{
    public Guid Id { get; set; }
    public string TraceId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string SpanId { get; set; } = "";
    public string? ParentSpanId { get; set; }
    public string? SourceEntityId { get; set; }
    public string? TargetEntityId { get; set; }
    public string EventType { get; set; } = "";
    public string Action { get; set; } = "";
    public ActivityStatus Status { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double? DurationMs { get; set; }
    public bool IsRoot { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
