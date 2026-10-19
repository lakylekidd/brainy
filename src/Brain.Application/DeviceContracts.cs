using System.Text.Json;
using Brain.Domain;

namespace Brain.Application;

public sealed record RegisterDeviceRequest(string Name, string DisplayName, string Hostname,
    string OperatingSystem, string Architecture, string ClientVersion, JsonElement? Metadata = null);
public sealed record UpdateDeviceRequest(string DisplayName);
public sealed record DeviceHeartbeatRequest(string ClientVersion, string? RuntimeAgentVersion,
    bool Healthy, JsonElement? Metadata = null);
public sealed record DeviceView(Guid Id, string Name, string DisplayName, string Hostname,
    string OperatingSystem, string Architecture, string ClientVersion, string? RuntimeAgentVersion,
    DeviceStatus Status, DateTimeOffset? LastSeenAt, string RegisteredBy, DateTimeOffset RegisteredAt,
    DateTimeOffset? RevokedAt, JsonElement Metadata);
public sealed record DeviceRegistration(DeviceView Device, string Credential, string[] Scopes);
public sealed record DeviceCredential(Guid DeviceId, string AgentId, string[] Scopes);

public interface IDeviceStore
{
    Task<DeviceCredential?> AuthenticateAsync(string credential, CancellationToken ct);
    Task<DeviceRegistration> RegisterAsync(string registeredBy, string agentId,
        string[] scopes, RegisterDeviceRequest request, CancellationToken ct);
    Task<IReadOnlyList<DeviceView>> ListAsync(string agentId, bool all, CancellationToken ct);
    Task<DeviceView?> GetAsync(Guid id, CancellationToken ct);
    Task<string?> AgentIdAsync(Guid id, CancellationToken ct);
    Task<DeviceView> RenameAsync(Guid id, string displayName, CancellationToken ct);
    Task<DeviceView> HeartbeatAsync(Guid id, DeviceHeartbeatRequest request, CancellationToken ct);
    Task<DeviceView> RevokeAsync(Guid id, CancellationToken ct);
    Task<DeviceRegistration> RotateAsync(Guid id, CancellationToken ct);
}
