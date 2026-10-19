using Brain.Domain;

namespace Brain.Application;

public sealed record RuntimeReportRequest(string EnvironmentId, ExecutionEnvironmentKind Kind,
    string OperatingSystem, string Architecture, bool DockerAvailable,
    Dictionary<string, string?> Commands, bool Healthy);
public sealed record RuntimeView(Guid DeviceId, string EnvironmentId, ExecutionEnvironmentKind Kind,
    string OperatingSystem, string Architecture, bool DockerAvailable,
    Dictionary<string, string?> Commands, bool Healthy, DateTimeOffset ReportedAt);

public interface IRuntimeStore
{
    Task<RuntimeView> ReportAsync(Guid deviceId, RuntimeReportRequest report, CancellationToken ct);
    Task<IReadOnlyList<RuntimeView>> ListAsync(Guid deviceId, CancellationToken ct);
}

public sealed class RuntimeService(IRuntimeStore store, IDeviceStore devices)
{
    public Task<RuntimeView> ReportAsync(Guid ownDeviceId, Guid id, RuntimeReportRequest request, CancellationToken ct)
    {
        if (ownDeviceId != id) throw new BrainException(403, "Only the bound device may report its runtime.");
        Validate(request);
        return store.ReportAsync(id, request, ct);
    }

    public async Task<IReadOnlyList<RuntimeView>> ListAsync(AgentContext actor, Guid? ownDeviceId,
        Guid id, CancellationToken ct)
    {
        actor.Require(Permissions.RuntimeRead);
        if (ownDeviceId != id && !actor.Has(Permissions.DevicesManage))
        {
            var owner = await devices.AgentIdAsync(id, ct);
            if (owner is null || owner != actor.Id) throw new BrainException(404, "Device not found.");
        }
        return await store.ListAsync(id, ct);
    }

    private static void Validate(RuntimeReportRequest request)
    {
        if (request.EnvironmentId is not { Length: > 0 and <= 100 } ||
            !request.EnvironmentId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ||
            request.OperatingSystem is not { Length: > 0 and <= 100 } ||
            request.Architecture is not { Length: > 0 and <= 20 } ||
            request.Commands is null || request.Commands.Count > 100 ||
            request.Commands.Any(x => x.Key.Length is < 1 or > 100 ||
                !x.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ||
                x.Value is { Length: > 200 }))
            throw new BrainException(400, "Invalid runtime report.");
    }
}
