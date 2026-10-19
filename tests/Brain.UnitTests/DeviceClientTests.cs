using Brain.Application;
using Brain.Client;
using Brain.Domain;

namespace Brain.UnitTests;

public sealed class DeviceClientTests
{
    [Fact]
    public async Task Registration_grants_only_allowlisted_scopes()
    {
        var store = new RecordingDeviceStore();
        var grants = new HashSet<string> { Permissions.DevicesRegister, Permissions.RegistryRead,
            Permissions.AgentsAdmin, Permissions.DevicesRevoke };
        var service = new DeviceService(store, new DirectoryStub(new AgentContext("operator", null, grants)));
        await service.RegisterAsync("human-subject", "operator", new("desktop-01", "Desktop", "desktop.local",
            "windows", "x64", "1.0"), CancellationToken.None);
        Assert.Equal([Permissions.RegistryRead], store.Scopes);
        Assert.Equal("human-subject", store.Subject);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("UPPER")]
    [InlineData("bad/name")]
    public async Task Registration_rejects_invalid_names(string name)
    {
        var service = new DeviceService(new RecordingDeviceStore(),
            new DirectoryStub(new AgentContext("operator", null, new HashSet<string> { Permissions.DevicesRegister })));
        await Assert.ThrowsAsync<BrainException>(() => service.RegisterAsync("subject", "operator",
            new(name, "Desktop", "desktop", "linux", "arm64", "1.0"), CancellationToken.None));
    }

    [Fact]
    public async Task Sync_preserves_local_modifications()
    {
        var root = Path.Combine(Path.GetTempPath(), "brain-skills-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal("updated", await SkillSynchronizer.InstallAsync("sample", "first",
                new("source1", Hash("first")), CancellationToken.None, root));
            var file = Path.Combine(root, "sample", "SKILL.md");
            await File.WriteAllTextAsync(file, "local edit");
            Assert.Equal("conflict", await SkillSynchronizer.InstallAsync("sample", "second",
                new("source2", Hash("second")), CancellationToken.None, root));
            Assert.Equal("local edit", await File.ReadAllTextAsync(file));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Service_templates_quote_executable_paths_without_shell_commands()
    {
        var executable = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "brain & tools", "brain"));
        var systemd = ServiceInstaller.SystemdUnit(executable);
        var launchd = ServiceInstaller.LaunchdPlist(executable);
        Assert.Contains("ExecStart=\"", systemd);
        Assert.Contains("\" agent run", systemd);
        Assert.Contains("brain &amp; tools", launchd);
        Assert.DoesNotContain("sh -c", systemd);
    }

    private static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(text)));

    private sealed class DirectoryStub(AgentContext agent) : IAgentDirectory
    {
        public Task<AgentContext?> ResolveAsync(string id, CancellationToken ct) => Task.FromResult(id == agent.Id ? agent : null);
        public Task<IReadOnlyList<AgentView>> ListAsync(AgentContext actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<AgentView> GetAsync(AgentContext actor, string id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<RoleView>> ListRolesAsync(AgentContext actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermissionView>> ListPermissionsAsync(AgentContext actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<AgentView> CreateAsync(AgentContext actor, CreateAgentRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<AgentView> UpdateAccessAsync(AgentContext actor, string id, UpdateAccessRequest request, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RecordingDeviceStore : IDeviceStore
    {
        public string[] Scopes { get; private set; } = [];
        public string Subject { get; private set; } = "";
        public Task<DeviceRegistration> RegisterAsync(string registeredBy, string agentId, string[] scopes,
            RegisterDeviceRequest request, CancellationToken ct)
        {
            Scopes = scopes; Subject = registeredBy;
            return Task.FromResult(new DeviceRegistration(null!, "credential", scopes));
        }
        public Task<DeviceCredential?> AuthenticateAsync(string credential, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<DeviceView>> ListAsync(string agentId, bool all, CancellationToken ct) => throw new NotImplementedException();
        public Task<DeviceView?> GetAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<string?> AgentIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<DeviceView> RenameAsync(Guid id, string displayName, CancellationToken ct) => throw new NotImplementedException();
        public Task<DeviceView> HeartbeatAsync(Guid id, DeviceHeartbeatRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<DeviceView> RevokeAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<DeviceRegistration> RotateAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }
}
