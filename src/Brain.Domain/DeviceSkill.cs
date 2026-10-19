namespace Brain.Domain;

public enum DeviceSkillStatus { Synced, Conflict, Stale }

public sealed class DeviceSkill
{
    public Guid DeviceId { get; set; }
    public string Harness { get; set; } = "";
    public string SkillId { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DeviceSkillStatus Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
