using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
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
    public async Task Openapi_describes_both_credentials_and_anonymous_liveness()
    {
        using var client = factory.Agent();
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal(2, schema.GetProperty("security")[0].EnumerateObject().Count());
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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Brain"] = "Host=localhost;Database=unused;Username=unused",
            ["Database:MigrateOnStartup"] = "false",
            ["Database:SeedDevelopment"] = "false",
            ["Authentication:AgentTokenHashes:codex"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services => services.AddSingleton<IAgentDirectory, ContractDirectory>());
    }
    public HttpClient Agent()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Agent-Id", "codex");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return client;
    }
    private sealed class ContractDirectory : IAgentDirectory
    {
        public Task<AgentContext?> ResolveAsync(string id, CancellationToken ct) => Task.FromResult<AgentContext?>(
            new(id, "platform", new HashSet<string> { Permissions.MemoryWrite }));
        public Task<IReadOnlyList<AgentView>> ListAsync(AgentContext actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentView> CreateAsync(AgentContext actor, CreateAgentRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentView> UpdateAccessAsync(AgentContext actor, string id, UpdateAccessRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
