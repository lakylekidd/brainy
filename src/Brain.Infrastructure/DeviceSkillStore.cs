using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class DeviceSkillStore(BrainDbContext db, TimeProvider time) : IDeviceSkillStore
{
    public async Task<IReadOnlyList<DeviceSkillView>> ListAsync(Guid deviceId, CancellationToken ct) =>
        (await db.DeviceSkills.AsNoTracking().Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.Harness).ThenBy(x => x.SkillId).ToListAsync(ct)).Select(View).ToArray();

    public async Task<IReadOnlyList<DeviceSkillView>> ReportAsync(Guid deviceId,
        IReadOnlyList<DeviceSkillReport> entries, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == deviceId && x.RevokedAt == null, ct)
            ?? throw new BrainException(404, "Device not found.");
        foreach (var entry in entries)
        {
            var state = await db.DeviceSkills.SingleOrDefaultAsync(x => x.DeviceId == deviceId &&
                x.Harness == entry.Harness && x.SkillId == entry.SkillId, ct);
            if (state is null)
            {
                state = new DeviceSkill { DeviceId = deviceId, Harness = entry.Harness, SkillId = entry.SkillId };
                db.DeviceSkills.Add(state);
            }
            state.SourceHash = entry.SourceHash;
            state.Status = entry.Status;
            state.UpdatedAt = time.GetUtcNow();
        }
        db.AuditEvents.Add(new AuditEvent { AgentId = device.AgentId,
            Action = entries.Any(x => x.Status != DeviceSkillStatus.Synced) ? "brain.skills.sync.failed" : "brain.skills.sync.completed",
            Resource = $"device:{deviceId}", Result = entries.Any(x => x.Status != DeviceSkillStatus.Synced) ? "conflict" : "success",
            Timestamp = time.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return await ListAsync(deviceId, ct);
    }

    private static DeviceSkillView View(DeviceSkill state) => new(state.DeviceId, state.Harness,
        state.SkillId, state.SourceHash, state.Status, state.UpdatedAt);
}
