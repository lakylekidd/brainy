namespace Brain.Domain;

public enum ExecutionEnvironmentKind { Host, Container, Remote, Harness }

public sealed class DeviceRuntime
{
    public Guid DeviceId { get; set; }
    public string EnvironmentId { get; set; } = "";
    public ExecutionEnvironmentKind Kind { get; set; }
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
    public bool DockerAvailable { get; set; }
    public string CommandsJson { get; set; } = "{}";
    public bool Healthy { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
}
