using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Brain.Application;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Brain.IntegrationTests;

public sealed class DeviceApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Device_credentials_are_scoped_rotatable_and_revocable()
    {
        DeviceRegistration registration;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            registration = await scope.ServiceProvider.GetRequiredService<DeviceService>().RegisterAsync(
                "oidc-subject", "willem-de-jong", new("desktop-01", "Desktop", "desktop.local",
                    "windows", "x64", "1.0"), CancellationToken.None);
        }
        Assert.StartsWith("bd_", registration.Credential);
        Assert.DoesNotContain("agents.admin", registration.Scopes);
        using var device = fixture.Factory.CreateClient();
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Credential);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync($"/api/devices/{registration.Device.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await device.GetAsync("/api/agents")).StatusCode);
        var heartbeat = await device.PostAsJsonAsync($"/api/devices/{registration.Device.Id}/heartbeat",
            new DeviceHeartbeatRequest("1.1", "1.0", true));
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        var heartbeatPath = $"/api/devices/{registration.Device.Id}/heartbeat";
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync(heartbeatPath,
            new { clientVersion = "1.1", runtimeAgentVersion = "1.0", healthy = true,
                metadata = new { installedHarnesses = new[] { "codex" }, connectedHarnesses = new[] { "codex" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync(heartbeatPath,
            new { clientVersion = "1.1", runtimeAgentVersion = "1.0", healthy = true,
                metadata = new { installedHarnesses = new[] { "codex" }, connectedHarnesses = Array.Empty<string>() } })).StatusCode);
        var rotation = await device.PostAsync($"/api/devices/{registration.Device.Id}/rotate", null);
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        using var rotated = System.Text.Json.JsonDocument.Parse(await rotation.Content.ReadAsStringAsync());
        var newCredential = rotated.RootElement.GetProperty("credential").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync($"/api/devices/{registration.Device.Id}")).StatusCode);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newCredential);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync($"/api/devices/{registration.Device.Id}")).StatusCode);
        var runtimeReport = new { environmentId = "host", kind = "host", operatingSystem = "windows",
            architecture = "x64", dockerAvailable = false,
            commands = new Dictionary<string, string?> { ["dotnet"] = "10.0" }, healthy = true };
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync($"/api/devices/{registration.Device.Id}/runtime", runtimeReport)).StatusCode);
        using var runtimeResponse = await device.GetAsync($"/api/devices/{registration.Device.Id}/runtime");
        Assert.Equal(HttpStatusCode.OK, runtimeResponse.StatusCode);
        using var runtimeJson = System.Text.Json.JsonDocument.Parse(await runtimeResponse.Content.ReadAsStringAsync());
        var host = Assert.Single(runtimeJson.RootElement.EnumerateArray());
        Assert.Equal("host", host.GetProperty("kind").GetString());
        Assert.Equal("10.0", host.GetProperty("commands").GetProperty("dotnet").GetString());
        var skillDirectory = Path.Combine(fixture.RegistryPath, "skills", "example");
        Directory.CreateDirectory(skillDirectory);
        await File.WriteAllTextAsync(Path.Combine(skillDirectory, "manifest.yaml"),
            "id: example\nname: Example\ndescription: Example skill.\nrequired_tools: []\nrequired_permissions: []\nruntime_requirements: [host:dotnet>=10.0, container:python3>=3.10]\n");
        await File.WriteAllTextAsync(Path.Combine(skillDirectory, "SKILL.md"), "# Example\n");
        using var dependencyResponse = await device.GetAsync($"/api/devices/{registration.Device.Id}/dependencies");
        Assert.Equal(HttpStatusCode.OK, dependencyResponse.StatusCode);
        using var dependencies = System.Text.Json.JsonDocument.Parse(await dependencyResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, dependencies.RootElement.GetArrayLength());
        Assert.True(dependencies.RootElement.EnumerateArray().First().GetProperty("available").GetBoolean());
        Assert.Equal("environment not reported", dependencies.RootElement.EnumerateArray().Last().GetProperty("reason").GetString());
        using var source = System.Text.Json.JsonDocument.Parse(await device.GetStringAsync("/api/skills/example"));
        var sourceHash = source.RootElement.GetProperty("skill").GetProperty("metadata").GetProperty("contentHash").GetString()!;
        var skillsPath = $"/api/devices/{registration.Device.Id}/skills/sync";
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync(skillsPath,
            new { entries = new[] { new { harness = "brain", skillId = "example", sourceHash, status = "synced" } } })).StatusCode);
        using var synced = System.Text.Json.JsonDocument.Parse(await device.GetStringAsync($"/api/devices/{registration.Device.Id}/skills"));
        Assert.Equal("synced", Assert.Single(synced.RootElement.EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync(skillsPath,
            new { entries = new[] { new { harness = "brain", skillId = "example",
                sourceHash = new string('0', 64), status = "synced" } } })).StatusCode);
        using var stale = System.Text.Json.JsonDocument.Parse(await device.GetStringAsync($"/api/devices/{registration.Device.Id}/skills"));
        Assert.Equal("stale", Assert.Single(stale.RootElement.EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsync($"/api/devices/{registration.Device.Id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync($"/api/devices/{registration.Device.Id}")).StatusCode);
        using var scope2 = fixture.Factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "brain.device.registered"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "brain.device.credential_rotated"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "brain.device.revoked"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "brain.harness.connected"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "brain.harness.disconnected"));
        Assert.DoesNotContain(registration.Credential, (await db.Devices.SingleAsync()).CredentialHash);
    }

    [Fact]
    public async Task Human_registration_rejects_agent_and_anonymous_credentials()
    {
        var body = new RegisterDeviceRequest("desktop-02", "Desktop", "desktop", "linux", "arm64", "1.0");
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/devices/register", body)).StatusCode);
        using var agent = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.PostAsJsonAsync("/api/devices/register", body)).StatusCode);
    }

    [Fact]
    public async Task Valid_human_oidc_token_registers_a_device_without_agent_token()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-oidc-signing-key-000000000000000"));
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Oidc:Authority"] = "https://issuer.test",
                ["Authentication:Oidc:Audience"] = "brain-api",
                ["Authentication:Oidc:ClientId"] = "brain-cli",
                ["Authentication:HumanAgentBindings:test-human"] = "willem-de-jong"
            }));
            builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>("HumanOidc", options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = "https://issuer.test" };
                configuration.SigningKeys.Add(key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
        });
        using var client = factory.CreateClient();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", "test-human"), new Claim("scope", "brain.device")]),
            Issuer = "https://issuer.test", Audience = "brain-api",
            Expires = DateTime.UtcNow.AddMinutes(5), SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(descriptor));
        var response = await client.PostAsJsonAsync("/api/devices/register",
            new RegisterDeviceRequest("oidc-desktop", "OIDC Desktop", "desktop", "linux", "x64", "1.0"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("bd_", body.RootElement.GetProperty("credential").GetString());
        Assert.Equal("test-human", body.RootElement.GetProperty("device").GetProperty("registeredBy").GetString());
    }

    [Fact]
    public async Task Provisioning_requires_allowlisted_package_and_human_approval()
    {
        DeviceRegistration registration;
        using (var scope = fixture.Factory.Services.CreateScope())
            registration = await scope.ServiceProvider.GetRequiredService<DeviceService>().RegisterAsync(
                "test-human", "willem-de-jong", new("provision-host", "Provision host", "host",
                    "linux", "x64", "1.0"), CancellationToken.None);
        var skillDirectory = Path.Combine(fixture.RegistryPath, "skills", "graphify");
        Directory.CreateDirectory(skillDirectory);
        await File.WriteAllTextAsync(Path.Combine(skillDirectory, "manifest.yaml"),
            "id: graphify\nname: Graphify\ndescription: Graphify skill.\nrequired_tools: []\nrequired_permissions: []\nruntime_requirements: [host:graphify>=1.2]\n");
        await File.WriteAllTextAsync(Path.Combine(skillDirectory, "SKILL.md"), "# Graphify\n");
        using var device = fixture.Factory.CreateClient();
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Credential);
        (await device.PostAsJsonAsync($"/api/devices/{registration.Device.Id}/runtime",
            new { environmentId = "host", kind = "host", operatingSystem = "linux",
                architecture = "x64", dockerAvailable = false,
                commands = new Dictionary<string, string?>(), healthy = true })).EnsureSuccessStatusCode();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-oidc-signing-key-000000000000000"));
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Oidc:Authority"] = "https://issuer.test",
                ["Authentication:Oidc:Audience"] = "brain-api",
                ["Authentication:Oidc:ClientId"] = "brain-cli",
                ["Authentication:HumanAgentBindings:test-human"] = "willem-de-jong",
                ["Provisioning:Packages:0:Requirement"] = "host:graphify>=1.2",
                ["Provisioning:Packages:0:PackageId"] = "graphify",
                ["Provisioning:Packages:0:Version"] = "1.2.0",
                ["Provisioning:Packages:0:WheelUrl"] = "https://packages.example/graphify-1.2.0-py3-none-any.whl",
                ["Provisioning:Packages:0:Sha256"] = new string('A', 64),
                ["Provisioning:Packages:0:OperatingSystem"] = "linux",
                ["Provisioning:Packages:0:Architecture"] = "x64"
            }));
            builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>("HumanOidc", options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = "https://issuer.test" };
                configuration.SigningKeys.Add(key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
        });
        using var human = factory.CreateClient();
        human.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor { Subject = new ClaimsIdentity([new Claim("sub", "test-human"),
                    new Claim("scope", "brain.device")]), Issuer = "https://issuer.test", Audience = "brain-api",
                Expires = DateTime.UtcNow.AddMinutes(5), SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256) }));
        var path = $"/api/devices/{registration.Device.Id}/provisioning";
        var planned = await human.PostAsJsonAsync(path, new { requirement = "host:graphify>=1.2" });
        Assert.Equal(HttpStatusCode.Created, planned.StatusCode);
        using var plannedJson = System.Text.Json.JsonDocument.Parse(await planned.Content.ReadAsStringAsync());
        var planId = plannedJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("pending", plannedJson.RootElement.GetProperty("status").GetString());
        using var before = System.Text.Json.JsonDocument.Parse(await device.GetStringAsync(path + "/queue"));
        Assert.Empty(before.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await human.PostAsync($"{path}/{planId}/approve", null)).StatusCode);
        using var queued = System.Text.Json.JsonDocument.Parse(await device.GetStringAsync(path + "/queue"));
        Assert.Single(queued.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsync($"{path}/{planId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsync($"{path}/{planId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync($"{path}/{planId}/complete",
            new { succeeded = true, result = "verified" })).StatusCode);
        using var scope2 = fixture.Factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "brain.runtime.provisioning.approved"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "brain.runtime.provisioning.completed"));
    }
}
