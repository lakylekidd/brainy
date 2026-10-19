using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public sealed class ProvisioningOptions
{
    public List<ApprovedPackage> Packages { get; set; } = [];
}

public sealed class ApprovedPackage
{
    public string Requirement { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string Version { get; set; } = "";
    public string WheelUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
}

public sealed record ProvisionRequest(string Requirement);
public sealed record ProvisionResultRequest(bool Succeeded, string Result);
public sealed record ProvisioningView(Guid Id, Guid DeviceId, string Requirement, string PackageId,
    string PackageVersion, string WheelUrl, string Sha256, string OperatingSystem, string Architecture,
    ProvisioningStatus Status, string RequestedBy, string? ApprovedBy, DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? Result);

public interface IProvisioningStore
{
    Task<ProvisioningView> CreateAsync(ProvisioningPlan plan, CancellationToken ct);
    Task<ProvisioningView> ApproveAsync(Guid deviceId, Guid planId, string approvedBy, CancellationToken ct);
    Task<IReadOnlyList<ProvisioningView>> ListAsync(Guid deviceId, CancellationToken ct);
    Task<IReadOnlyList<ProvisioningView>> QueueAsync(Guid deviceId, CancellationToken ct);
    Task<ProvisioningView> StartAsync(Guid deviceId, Guid planId, CancellationToken ct);
    Task<ProvisioningView> CompleteAsync(Guid deviceId, Guid planId, ProvisionResultRequest result, CancellationToken ct);
}

public sealed class ProvisioningService(ProvisioningOptions options, IProvisioningStore store,
    IDeviceStore devices, DependencyService dependencies, TimeProvider time)
{
    private static readonly Regex PackageId = new("^[a-z0-9][a-z0-9._-]{0,99}$", RegexOptions.Compiled);
    private static readonly Regex Version = new("^[0-9]+(?:[.][0-9]+){1,3}$", RegexOptions.Compiled);

    public async Task<ProvisioningView> PlanAsync(AgentContext actor, Guid deviceId,
        ProvisionRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.RuntimeProvision);
        if (string.IsNullOrWhiteSpace(request.Requirement)) throw new BrainException(400, "Requirement is required.");
        var owner = await devices.AgentIdAsync(deviceId, ct);
        if (owner is null || owner != actor.Id && !actor.Has(Permissions.AgentsAdmin))
            throw new BrainException(404, "Device not found.");
        var missing = await dependencies.CheckAsync(actor, null, deviceId, ct);
        if (!missing.Any(x => x.Requirement == request.Requirement && !x.Available))
            throw new BrainException(409, "Requirement is not missing from an authorized skill.");
        var device = await devices.GetAsync(deviceId, ct) ?? throw new BrainException(404, "Device not found.");
        if (device.RevokedAt is not null) throw new BrainException(409, "Device is revoked.");
        var matches = options.Packages.Where(x => x.Requirement == request.Requirement &&
            x.OperatingSystem == device.OperatingSystem && x.Architecture == device.Architecture).Take(2).ToArray();
        if (matches.Length != 1) throw new BrainException(422, "Exactly one approved package must match the requirement and device platform.");
        var package = matches[0];
        Validate(package);
        return await store.CreateAsync(new ProvisioningPlan
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Requirement = request.Requirement,
            PackageId = package.PackageId, PackageVersion = package.Version,
            WheelUrl = package.WheelUrl, Sha256 = package.Sha256.ToUpperInvariant(),
            OperatingSystem = package.OperatingSystem, Architecture = package.Architecture,
            RequestedBy = actor.Id, CreatedAt = time.GetUtcNow(), Status = ProvisioningStatus.Pending
        }, ct);
    }

    public async Task<ProvisioningView> ApproveAsync(AgentContext actor, string subject, Guid deviceId,
        Guid planId, CancellationToken ct)
    {
        actor.Require(Permissions.RuntimeProvision);
        var owner = await devices.AgentIdAsync(deviceId, ct);
        if (owner is null || owner != actor.Id && !actor.Has(Permissions.DevicesManage))
            throw new BrainException(404, "Device not found.");
        return await store.ApproveAsync(deviceId, planId, subject, ct);
    }

    public async Task<IReadOnlyList<ProvisioningView>> ListAsync(AgentContext actor, Guid? ownDeviceId,
        Guid deviceId, CancellationToken ct)
    {
        actor.Require(Permissions.RuntimeRead);
        if (ownDeviceId != deviceId)
        {
            var owner = await devices.AgentIdAsync(deviceId, ct);
            if (owner is null || owner != actor.Id && !actor.Has(Permissions.DevicesManage))
                throw new BrainException(404, "Device not found.");
        }
        return await store.ListAsync(deviceId, ct);
    }

    public Task<IReadOnlyList<ProvisioningView>> QueueAsync(Guid ownDeviceId, Guid deviceId, CancellationToken ct)
    {
        if (ownDeviceId != deviceId) throw new BrainException(403, "Only the bound device may poll provisioning.");
        return store.QueueAsync(deviceId, ct);
    }

    public Task<ProvisioningView> StartAsync(Guid ownDeviceId, Guid deviceId, Guid planId, CancellationToken ct)
    {
        if (ownDeviceId != deviceId) throw new BrainException(403, "Only the bound device may start provisioning.");
        return store.StartAsync(deviceId, planId, ct);
    }

    public Task<ProvisioningView> CompleteAsync(Guid ownDeviceId, Guid deviceId, Guid planId,
        ProvisionResultRequest result, CancellationToken ct)
    {
        if (ownDeviceId != deviceId) throw new BrainException(403, "Only the bound device may finish provisioning.");
        if (result.Result is null || result.Result.Length > 200 || result.Result.Any(char.IsControl))
            throw new BrainException(400, "Invalid provisioning result.");
        return store.CompleteAsync(deviceId, planId, result, ct);
    }

    private static void Validate(ApprovedPackage package)
    {
        if (!PackageId.IsMatch(package.PackageId) || !Version.IsMatch(package.Version) ||
            package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit) ||
            !Uri.TryCreate(package.WheelUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            !uri.AbsolutePath.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
            throw new BrainException(422, "Invalid approved package policy.");
    }
}
