using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class DeviceStore(BrainDbContext db, TimeProvider time) : IDeviceStore
{
    public async Task<DeviceCredential?> AuthenticateAsync(string credential, CancellationToken ct)
    {
        if (!credential.StartsWith("bd_", StringComparison.Ordinal) || credential.Length != 46)
            return null;
        var hash = Hash(credential);
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.CredentialHash == hash, ct);
        if (device is null || device.RevokedAt is not null) return null;
        return new(device.Id, device.AgentId, device.Scopes);
    }

    public async Task<DeviceRegistration> RegisterAsync(string registeredBy, string agentId,
        string[] scopes, RegisterDeviceRequest request, CancellationToken ct)
    {
        var token = NewCredential();
        var device = new Device
        {
            Id = Guid.NewGuid(), Name = request.Name, DisplayName = request.DisplayName.Trim(),
            Hostname = request.Hostname.Trim(), OperatingSystem = request.OperatingSystem,
            Architecture = request.Architecture, ClientVersion = request.ClientVersion,
            RegisteredBy = registeredBy, RegisteredAt = time.GetUtcNow(), AgentId = agentId,
            CredentialHash = Hash(token), Scopes = scopes,
            MetadataJson = request.Metadata?.GetRawText() ?? "{}"
        };
        db.Devices.Add(device);
        Audit(agentId, "brain.device.registered", device.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new BrainException(409, "Device name is already registered."); }
        return new(View(device), token, scopes);
    }

    public async Task<IReadOnlyList<DeviceView>> ListAsync(string agentId, bool all, CancellationToken ct)
    {
        var query = db.Devices.AsNoTracking();
        if (!all) query = query.Where(x => x.AgentId == agentId);
        return (await query.OrderBy(x => x.Name).Take(1000).ToListAsync(ct)).Select(View).ToArray();
    }

    public async Task<DeviceView?> GetAsync(Guid id, CancellationToken ct) =>
        (await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)) is { } device ? View(device) : null;

    public Task<string?> AgentIdAsync(Guid id, CancellationToken ct) =>
        db.Devices.Where(x => x.Id == id).Select(x => x.AgentId).SingleOrDefaultAsync(ct);

    public async Task<DeviceView> RenameAsync(Guid id, string displayName, CancellationToken ct)
    {
        var device = await RequireAsync(id, ct);
        device.DisplayName = displayName;
        Audit(device.AgentId, "brain.device.renamed", id);
        await db.SaveChangesAsync(ct);
        return View(device);
    }

    public async Task<DeviceView> HeartbeatAsync(Guid id, DeviceHeartbeatRequest request, CancellationToken ct)
    {
        var device = await RequireAsync(id, ct);
        device.ClientVersion = request.ClientVersion;
        device.RuntimeAgentVersion = request.RuntimeAgentVersion;
        device.LastSeenAt = time.GetUtcNow();
        device.Status = request.Healthy ? DeviceStatus.Online : DeviceStatus.Degraded;
        if (request.Metadata is { } metadata)
        {
            var previous = ConnectedHarnesses(JsonSerializer.Deserialize<JsonElement>(device.MetadataJson));
            var connected = ConnectedHarnesses(metadata);
            foreach (var name in connected.Except(previous, StringComparer.Ordinal))
                Audit(device.AgentId, "brain.harness.connected", id, $"/harness:{name}");
            foreach (var name in previous.Except(connected, StringComparer.Ordinal))
                Audit(device.AgentId, "brain.harness.disconnected", id, $"/harness:{name}");
            device.MetadataJson = metadata.GetRawText();
        }
        Audit(device.AgentId, "brain.device.heartbeat", id);
        await db.SaveChangesAsync(ct);
        return View(device);
    }

    public async Task<DeviceView> RevokeAsync(Guid id, CancellationToken ct)
    {
        var device = await RequireAsync(id, ct);
        device.RevokedAt = time.GetUtcNow();
        device.Status = DeviceStatus.Revoked;
        device.CredentialHash = $"revoked:{device.Id:N}";
        Audit(device.AgentId, "brain.device.revoked", id);
        await db.SaveChangesAsync(ct);
        return View(device);
    }

    public async Task<DeviceRegistration> RotateAsync(Guid id, CancellationToken ct)
    {
        var device = await RequireAsync(id, ct);
        var token = NewCredential();
        device.CredentialHash = Hash(token);
        Audit(device.AgentId, "brain.device.credential_rotated", id);
        await db.SaveChangesAsync(ct);
        return new(View(device), token, device.Scopes);
    }

    private async Task<Device> RequireAsync(Guid id, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (device is null) throw new BrainException(404, "Device not found.");
        if (device.RevokedAt is not null) throw new BrainException(409, "Device is revoked.");
        return device;
    }

    private static string[] ConnectedHarnesses(JsonElement metadata) =>
        metadata.ValueKind == JsonValueKind.Object &&
        metadata.TryGetProperty("connectedHarnesses", out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!).ToArray() : [];

    private void Audit(string agentId, string action, Guid id, string suffix = "") => db.AuditEvents.Add(new AuditEvent
    {
        AgentId = agentId, Action = action, Resource = $"device:{id}{suffix}", Result = "success", Timestamp = time.GetUtcNow()
    });

    private static string NewCredential() => "bd_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private DeviceView View(Device d) => new(d.Id, d.Name, d.DisplayName, d.Hostname,
        d.OperatingSystem, d.Architecture, d.ClientVersion, d.RuntimeAgentVersion,
        d.RevokedAt is not null ? DeviceStatus.Revoked : d.Status == DeviceStatus.Online &&
            d.LastSeenAt < time.GetUtcNow().AddMinutes(-5) ? DeviceStatus.Offline : d.Status,
        d.LastSeenAt, d.RegisteredBy, d.RegisteredAt, d.RevokedAt,
        JsonSerializer.Deserialize<JsonElement>(d.MetadataJson));
}
