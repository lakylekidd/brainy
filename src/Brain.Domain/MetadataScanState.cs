namespace Brain.Domain;

public sealed class MetadataScanState
{
    public string Path { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string RevisionToken { get; set; } = "";
    public int SchemaVersion { get; set; }
    public string PolicyHash { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; }
    public DateTimeOffset? LastEnrichedAt { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public Guid? ProposalId { get; set; }
    public string IssuesJson { get; set; } = "[]";
    public string? LastError { get; set; }
}
