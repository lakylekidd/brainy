namespace Brain.Domain;

public enum ProvisioningStatus { Pending, Approved, Running, Completed, Failed }

public sealed class ProvisioningPlan
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Requirement { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string PackageVersion { get; set; } = "";
    public string WheelUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
    public ProvisioningStatus Status { get; set; }
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Result { get; set; }
}
