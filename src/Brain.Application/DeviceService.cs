using System.Text.Json;
using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public sealed class DeviceService(IDeviceStore store, IAgentDirectory agents)
{
    private static readonly Regex NamePattern = new("^[a-z0-9][a-z0-9-]{0,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // Device credentials can only carry these read and client-operation grants.
    private static readonly string[] DeviceScopeAllowlist =
        [Permissions.KnowledgeRead, Permissions.RegistryRead, Permissions.MemoryRead,
         Permissions.MemoryWrite, Permissions.DevicesRead, Permissions.RuntimeRead,
         Permissions.HarnessesConnect, Permissions.SkillsSync];

    public async Task<DeviceRegistration> RegisterAsync(string subject, string agentId,
        RegisterDeviceRequest request, CancellationToken ct)
    {
        var agent = await agents.ResolveAsync(agentId, ct) ?? throw new BrainException(403, "Human binding is not enabled.");
        agent.Require(Permissions.DevicesRegister);
        Validate(request);
        var scopes = DeviceScopeAllowlist.Where(agent.Has).ToArray();
        return await store.RegisterAsync(subject, agent.Id, scopes, request, ct);
    }

    public Task<IReadOnlyList<DeviceView>> ListAsync(AgentContext actor, CancellationToken ct)
    {
        actor.Require(Permissions.DevicesRead);
        return store.ListAsync(actor.Id, actor.Has(Permissions.DevicesManage), ct);
    }

    public async Task<DeviceView> GetAsync(AgentContext actor, Guid? ownDeviceId, Guid id, CancellationToken ct)
    {
        actor.Require(Permissions.DevicesRead);
        await AuthorizeAsync(actor, ownDeviceId, id, Permissions.DevicesManage, ct);
        return await store.GetAsync(id, ct) ?? throw new BrainException(404, "Device not found.");
    }

    public async Task<DeviceView> RenameAsync(AgentContext actor, Guid? ownDeviceId, Guid id,
        UpdateDeviceRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ownDeviceId, id, Permissions.DevicesManage, ct);
        ValidateText(request.DisplayName, nameof(request.DisplayName), 200);
        return await store.RenameAsync(id, request.DisplayName.Trim(), ct);
    }

    public async Task<DeviceView> HeartbeatAsync(AgentContext actor, Guid? ownDeviceId, Guid id,
        DeviceHeartbeatRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ownDeviceId, id, Permissions.DevicesManage, ct);
        ValidateText(request.ClientVersion, nameof(request.ClientVersion), 100);
        if (request.RuntimeAgentVersion is not null)
            ValidateText(request.RuntimeAgentVersion, nameof(request.RuntimeAgentVersion), 100);
        ValidateMetadata(request.Metadata);
        return await store.HeartbeatAsync(id, request, ct);
    }

    public async Task<DeviceView> RevokeAsync(AgentContext actor, Guid? ownDeviceId, Guid id, CancellationToken ct)
    {
        if (ownDeviceId != id)
        {
            actor.Require(Permissions.DevicesRevoke);
            await AuthorizeAsync(actor, null, id, Permissions.DevicesManage, ct);
        }
        return await store.RevokeAsync(id, ct);
    }

    public async Task<DeviceRegistration> RotateAsync(AgentContext actor, Guid? ownDeviceId, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ownDeviceId, id, Permissions.DevicesManage, ct);
        return await store.RotateAsync(id, ct);
    }

    private async Task AuthorizeAsync(AgentContext actor, Guid? ownDeviceId, Guid id,
        string permission, CancellationToken ct)
    {
        if (ownDeviceId == id) return;
        actor.Require(permission);
        var owner = await store.AgentIdAsync(id, ct);
        if (owner is null || owner != actor.Id && !actor.Has(Permissions.AgentsAdmin))
            throw new BrainException(404, "Device not found.");
    }

    private static void Validate(RegisterDeviceRequest request)
    {
        if (request.Name is null || !NamePattern.IsMatch(request.Name))
            throw new BrainException(400, "Invalid device name.");
        ValidateText(request.DisplayName, nameof(request.DisplayName), 200);
        ValidateText(request.Hostname, nameof(request.Hostname), 255);
        if (request.OperatingSystem is not ("windows" or "linux" or "macos"))
            throw new BrainException(400, "Unsupported operating system.");
        if (request.Architecture is not ("x64" or "arm64"))
            throw new BrainException(400, "Unsupported architecture.");
        ValidateText(request.ClientVersion, nameof(request.ClientVersion), 100);
        ValidateMetadata(request.Metadata);
    }

    private static void ValidateText(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new BrainException(400, $"Invalid {field}.");
    }

    private static void ValidateMetadata(JsonElement? metadata)
    {
        if (metadata is not { } value) return;
        if (value.ValueKind != JsonValueKind.Object || value.GetRawText().Length > 4096)
            throw new BrainException(400, "Invalid device metadata.");
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name is not ("installedHarnesses" or "connectedHarnesses") || property.Value.ValueKind != JsonValueKind.Array ||
                property.Value.GetArrayLength() > 20 || property.Value.EnumerateArray().Any(x =>
                    x.ValueKind != JsonValueKind.String || x.GetString() is not { } name || !NamePattern.IsMatch(name)))
                throw new BrainException(400, "Invalid device metadata.");
        }
    }
}
