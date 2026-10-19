using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

// Transport checks run without PostgreSQL; the memory acceptance tests still require real pgvector.
public sealed class ApiContractTests : IClassFixture<ContractFactory>
{
    private readonly ContractFactory factory;
    public ApiContractTests(ContractFactory factory) => this.factory = factory;

    [Fact]
    public async Task Authenticated_context_comes_from_bound_identity()
    {
        using var client = factory.Agent();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/agents/me");
        Assert.Equal("codex", me.GetProperty("id").GetString());
        client.DefaultRequestHeaders.Remove("X-Agent-Id");
        client.DefaultRequestHeaders.Add("X-Agent-Id", "administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents/me")).StatusCode);
    }

    [Fact]
    public async Task Administrator_token_resolves_server_configured_identity_and_current_grants()
    {
        using var client = factory.Administrator();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/agents/me");
        Assert.Equal("ui-admin", me.GetProperty("id").GetString());
        Assert.Equal(Permissions.MemoryWrite,
            Assert.Single(me.GetProperty("permissions").EnumerateArray()).GetString());
        client.DefaultRequestHeaders.Add("X-Agent-Id", "codex");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents/me")).StatusCode);
    }

    [Theory]
    [InlineData("/api/agents/me")]
    [InlineData("/api/agents")]
    [InlineData("/health/ready")]
    public async Task Invalid_administrator_token_cannot_enter_protected_endpoints(string path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new string('x', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Agent_token_without_identity_and_administrator_token_on_mcp_are_rejected()
    {
        using var agent = factory.Agent();
        agent.DefaultRequestHeaders.Remove("X-Agent-Id");
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/agents/me")).StatusCode);
        using var admin = factory.Administrator();
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsync("/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("disabled-admin")]
    public async Task Unconfigured_or_disabled_administrator_access_is_rejected(string? identity)
    {
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:UiAdminAgentId"] = identity,
                ["Authentication:UiAdminTokenHash"] = identity is null ? null : ContractFactory.AdminTokenHash
            })));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ContractFactory.AdminToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents/me")).StatusCode);
    }

    [Fact]
    public async Task Openapi_describes_both_credentials_and_anonymous_liveness()
    {
        using var client = factory.Agent();
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal(2, schema.GetProperty("security")[0].EnumerateObject().Count());
        Assert.Single(schema.GetProperty("security")[1].EnumerateObject());
        Assert.True(schema.GetProperty("security")[1].TryGetProperty("uiAdminToken", out _));
        Assert.Empty(schema.GetProperty("paths").GetProperty("/health/live").GetProperty("get").GetProperty("security").EnumerateArray());
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Theory]
    [InlineData("{\"memoryType\":999,\"subject\":\"subject\",\"content\":\"content\"}")]
    [InlineData("{\"memoryType\":\"unknown\",\"subject\":\"subject\",\"content\":\"content\"}")]
    [InlineData("{\"memoryType\":\"fact\",\"subject\":\"subject\",\"content\":\"content\",\"agentId\":\"someone-else\"}")]
    public async Task Invalid_contracts_are_rejected_before_persistence(string body)
    {
        using var client = factory.Agent();
        var response = await client.PostAsync("/api/memories", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ContractFactory : WebApplicationFactory<Program>
{
    private const string Token = "contract-test-credential-000000000000000000";
    public const string AdminToken = "contract-admin-credential-000000000000000000";
    public static string AdminTokenHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AdminToken)));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Brain"] = "Host=localhost;Database=unused;Username=unused",
            ["Database:MigrateOnStartup"] = "false",
            ["Database:SeedDevelopment"] = "false",
            ["Authentication:UiAdminAgentId"] = "ui-admin",
            ["Authentication:UiAdminTokenHash"] = AdminTokenHash,
            ["Authentication:AgentTokenHashes:codex"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAgentDirectory, ContractDirectory>();
            var dispatch = services.Single(d => d.ImplementationType == typeof(ActivityDispatchService));
            services.Remove(dispatch);
        });
    }
    public HttpClient Agent()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Agent-Id", "codex");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return client;
    }
    public HttpClient Administrator()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        return client;
    }
    private sealed class ContractDirectory : IAgentDirectory
    {
        public Task<AgentContext?> ResolveAsync(string id, CancellationToken ct) => Task.FromResult<AgentContext?>(
            id == "disabled-admin" ? null : new(id, "platform", new HashSet<string> { Permissions.MemoryWrite }));
        public Task<IReadOnlyList<AgentView>> ListAsync(AgentContext actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentView> GetAsync(AgentContext actor, string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoleView>> ListRolesAsync(AgentContext actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<RoleView> CreateRoleAsync(AgentContext actor, CreateRoleRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PermissionView>> ListPermissionsAsync(AgentContext actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentView> CreateAsync(AgentContext actor, CreateAgentRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentView> UpdateAccessAsync(AgentContext actor, string id, UpdateAccessRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
