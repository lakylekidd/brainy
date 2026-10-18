namespace Brain.Domain;

public enum DeviceStatus { Registered, Online, Offline, Degraded, Revoked }

public sealed class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string ClientVersion { get; set; } = "";
    public string? RuntimeAgentVersion { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Registered;
    public DateTimeOffset? LastSeenAt { get; set; }
    public string RegisteredBy { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string AgentId { get; set; } = "";
    public string CredentialHash { get; set; } = "";
    public string[] Scopes { get; set; } = [];
    public string MetadataJson { get; set; } = "{}";
}
