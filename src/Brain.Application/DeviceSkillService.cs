using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public sealed record DeviceSkillReport(string Harness, string SkillId, string SourceHash, DeviceSkillStatus Status);
public sealed record DeviceSkillSyncRequest(DeviceSkillReport[] Entries);
public sealed record DeviceSkillView(Guid DeviceId, string Harness, string SkillId, string SourceHash,
    DeviceSkillStatus Status, DateTimeOffset UpdatedAt);

public interface IDeviceSkillStore
{
    Task<IReadOnlyList<DeviceSkillView>> ListAsync(Guid deviceId, CancellationToken ct);
    Task<IReadOnlyList<DeviceSkillView>> ReportAsync(Guid deviceId, IReadOnlyList<DeviceSkillReport> entries, CancellationToken ct);
}

public sealed class DeviceSkillService(IDeviceSkillStore store, IDeviceStore devices, RegistryService registry)
{
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,99}$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<DeviceSkillView>> ListAsync(AgentContext actor, Guid? ownDeviceId,
        Guid deviceId, CancellationToken ct)
    {
        actor.Require(Permissions.DevicesRead);
        if (ownDeviceId != deviceId && !actor.Has(Permissions.DevicesManage))
        {
            var owner = await devices.AgentIdAsync(deviceId, ct);
            if (owner is null || owner != actor.Id) throw new BrainException(404, "Device not found.");
        }
        return await store.ListAsync(deviceId, ct);
    }

    public async Task<IReadOnlyList<DeviceSkillView>> ReportAsync(AgentContext actor, Guid? ownDeviceId,
        Guid deviceId, DeviceSkillSyncRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.SkillsSync);
        if (ownDeviceId != deviceId) throw new BrainException(403, "Only the bound device may report skill synchronization.");
        if (request.Entries is null || request.Entries.Length > 500 ||
            request.Entries.Any(e => e.Harness is not ("brain" or "claude-code" or "codex" or "hermes") ||
                e.SkillId is null || !IdPattern.IsMatch(e.SkillId) ||
                e.SourceHash is null || e.SourceHash.Length != 64 || !e.SourceHash.All(Uri.IsHexDigit)) ||
            request.Entries.Select(e => (e.Harness, e.SkillId)).Distinct().Count() != request.Entries.Length)
            throw new BrainException(400, "Invalid skill synchronization report.");
        var allowed = await registry.ListAsync(actor, RegistryKind.Skill, ct);
        foreach (var entry in request.Entries)
            if (allowed.All(skill => skill.Id != entry.SkillId))
                throw new BrainException(404, "Skill not found.");
        var normalized = request.Entries.Select(entry =>
        {
            var current = allowed.Single(skill => skill.Id == entry.SkillId);
            return entry with { Status = entry.Status == DeviceSkillStatus.Synced &&
                !string.Equals(entry.SourceHash, current.Metadata.ContentHash, StringComparison.OrdinalIgnoreCase)
                    ? DeviceSkillStatus.Stale : entry.Status };
        }).ToArray();
        return await store.ReportAsync(deviceId, normalized, ct);
    }
}
