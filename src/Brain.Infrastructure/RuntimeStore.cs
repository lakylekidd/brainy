using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class RuntimeStore(BrainDbContext db, TimeProvider time) : IRuntimeStore
{
    public async Task<RuntimeView> ReportAsync(Guid deviceId, RuntimeReportRequest report, CancellationToken ct)
    {
        if (!await db.Devices.AnyAsync(x => x.Id == deviceId && x.RevokedAt == null, ct))
            throw new BrainException(404, "Device not found.");
        var runtime = await db.DeviceRuntimes.SingleOrDefaultAsync(x => x.DeviceId == deviceId &&
            x.EnvironmentId == report.EnvironmentId, ct);
        if (runtime is null)
        {
            runtime = new DeviceRuntime { DeviceId = deviceId, EnvironmentId = report.EnvironmentId };
            db.DeviceRuntimes.Add(runtime);
        }
        runtime.Kind = report.Kind;
        runtime.OperatingSystem = report.OperatingSystem;
        runtime.Architecture = report.Architecture;
        runtime.DockerAvailable = report.DockerAvailable;
        runtime.CommandsJson = JsonSerializer.Serialize(report.Commands);
        runtime.Healthy = report.Healthy;
        runtime.ReportedAt = time.GetUtcNow();
        db.AuditEvents.Add(new AuditEvent { AgentId = (await db.Devices.Where(x => x.Id == deviceId)
            .Select(x => x.AgentId).SingleAsync(ct)), Action = "brain.runtime.heartbeat",
            Resource = $"device:{deviceId}/runtime:{report.EnvironmentId}", Result = "success", Timestamp = runtime.ReportedAt });
        await db.SaveChangesAsync(ct);
        return View(runtime);
    }

    public async Task<IReadOnlyList<RuntimeView>> ListAsync(Guid deviceId, CancellationToken ct) =>
        (await db.DeviceRuntimes.AsNoTracking().Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.EnvironmentId).ToListAsync(ct)).Select(View).ToArray();

    private static RuntimeView View(DeviceRuntime runtime) => new(runtime.DeviceId, runtime.EnvironmentId,
        runtime.Kind, runtime.OperatingSystem, runtime.Architecture, runtime.DockerAvailable,
        JsonSerializer.Deserialize<Dictionary<string, string?>>(runtime.CommandsJson) ?? [],
        runtime.Healthy, runtime.ReportedAt);
}
