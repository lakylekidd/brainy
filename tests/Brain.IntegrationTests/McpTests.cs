using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Brain.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Brain.IntegrationTests;

public sealed class McpTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false) } };
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task Official_client_discovers_eight_typed_tools_on_both_protocol_generations(string version)
    {
        using var http = fixture.Factory.Agent("codex");
        await using var client = await Connect(http, version);
        Assert.Equal("homelab-brain", client.ServerInfo.Name);
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "brain_find_capabilities", "brain_find_skills", "brain_get_skill", "brain_propose_knowledge_change",
            "brain_read_document", "brain_remember", "brain_search", "brain_search_memories" }, tools.Select(t => t.Name).Order());
        foreach (var tool in tools)
        {
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            Assert.Equal("object", tool.ProtocolTool.OutputSchema.Value.GetProperty("type").GetString());
            var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
            Assert.False(properties.TryGetProperty("agentId", out _));
            Assert.False(properties.TryGetProperty("current", out _));
            Assert.False(properties.TryGetProperty("cancellationToken", out _));
            Assert.False(tool.ProtocolTool.Annotations!.DestructiveHint);
        }
        var remembered = Read<MemoryView>(await client.CallToolAsync("brain_remember", new Dictionary<string, object?>
        { ["subject"] = "Protocol round trip", ["content"] = "Testing " + version, ["memoryType"] = "fact" }));
        Assert.Equal("codex", remembered.AgentId);
        Assert.Equal(MemoryType.Fact, remembered.MemoryType);
        Assert.Equal(MemoryVisibility.Private, remembered.Visibility);
        Assert.Single(Read<Brain.Mcp.MemorySearchResponse>(await client.CallToolAsync("brain_search_memories",
            new Dictionary<string, object?> { ["query"] = version, ["subject"] = "Protocol round trip" })).Memories);
    }

    [Fact]
    public async Task Memory_identity_privacy_and_sharing_match_http_across_interleaved_clients()
    {
        using var codexHttp = fixture.Factory.Agent("codex");
        using var adminHttp = fixture.Factory.Agent("willem-de-jong");
        await using var codex = await Connect(codexHttp);
        await using var admin = await Connect(adminHttp);
        var memory = Read<MemoryView>(await codex.CallToolAsync("brain_remember", new Dictionary<string, object?>
        { ["subject"] = "Private cluster observation", ["content"] = "Corosync configuration is missing.", ["metadata"] = new { host = "nacht-02" } }));
        var viaHttp = (await codexHttp.GetFromJsonAsync<MemoryView>($"/api/memories/{memory.Id}", Json))!;
        Assert.Equal(memory.Content, viaHttp.Content);
        Assert.Equal("nacht-02", viaHttp.Metadata.GetProperty("host").GetString());
        var privateSearch = Read<Brain.Mcp.MemorySearchResponse>(await admin.CallToolAsync("brain_search_memories",
            new Dictionary<string, object?> { ["query"] = "corosync", ["subject"] = "Private cluster observation" }));
        Assert.Empty(privateSearch.Memories);
        Read<MemoryView>(await admin.CallToolAsync("brain_remember", new Dictionary<string, object?>
        { ["subject"] = "Shared cluster recovery", ["content"] = "Restore corosync configuration.", ["visibility"] = "organization" }));
        var shared = Read<Brain.Mcp.MemorySearchResponse>(await codex.CallToolAsync("brain_search_memories",
            new Dictionary<string, object?> { ["query"] = "corosync", ["subject"] = "Shared cluster recovery" }));
        Assert.Equal("willem-de-jong", Assert.Single(shared.Memories).Metadata.AgentId);
        Assert.Equal("codex", Read<MemoryView>(await codex.CallToolAsync("brain_remember", new Dictionary<string, object?>
        { ["subject"] = "Still Codex", ["content"] = "Identity remains request scoped." })).AgentId);
        var concurrent = await Task.WhenAll(
            codex.CallToolAsync("brain_remember", new Dictionary<string, object?> { ["subject"] = "Concurrent Codex", ["content"] = "Keep this identity scoped." }).AsTask(),
            admin.CallToolAsync("brain_remember", new Dictionary<string, object?> { ["subject"] = "Concurrent admin", ["content"] = "Keep this other identity scoped." }).AsTask());
        Assert.Equal("codex", Read<MemoryView>(concurrent[0]).AgentId);
        Assert.Equal("willem-de-jong", Read<MemoryView>(concurrent[1]).AgentId);
    }

    [Fact]
    public async Task Document_read_and_pending_proposal_use_the_existing_review_writer_and_revision_flow()
    {
        const string before = "# Cluster\n## Recovery\nRestore corosync.\n";
        File.WriteAllText(Path.Combine(fixture.VaultPath, "cluster.md"), before);
        await TestGit.InitializeAsync(fixture.VaultPath);
        using var http = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await using var mcp = await Connect(http);
        var document = Read<KnowledgeDocumentView>(await mcp.CallToolAsync("brain_read_document", new Dictionary<string, object?> { ["path"] = "cluster.md" }));
        var proposal = Read<KnowledgeProposalView>(await mcp.CallToolAsync("brain_propose_knowledge_change", new Dictionary<string, object?>
        { ["path"] = "cluster.md", ["operation"] = "append", ["content"] = "Check quorum afterward.", ["reason"] = "Observed recovery", ["revisionToken"] = document.RevisionToken }));
        Assert.Equal(KnowledgeProposalStatus.Pending, proposal.Status);
        Assert.Equal("codex", proposal.AgentId);
        Assert.Equal(before, File.ReadAllText(Path.Combine(fixture.VaultPath, "cluster.md")));
        using (var scope = fixture.Factory.Services.CreateScope())
            Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<KnowledgeWriterService>().ProcessAsync()).Applied);
        (await admin.PostAsJsonAsync($"/api/knowledge/proposals/{proposal.Id}/approve", new ReviewKnowledgeProposalRequest(proposal.Version, "Reviewed MCP proposal"))).EnsureSuccessStatusCode();
        using (var scope = fixture.Factory.Services.CreateScope())
            Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<KnowledgeWriterService>().ProcessAsync()).Applied);
        var after = Read<KnowledgeDocumentView>(await mcp.CallToolAsync("brain_read_document", new Dictionary<string, object?> { ["path"] = "cluster.md" }));
        Assert.Contains("Check quorum afterward.", after.Content);
        Assert.NotEqual(document.RevisionToken, after.RevisionToken);
        AssertError(await mcp.CallToolAsync("brain_propose_knowledge_change", new Dictionary<string, object?>
        { ["path"] = "cluster.md", ["operation"] = "append", ["content"] = "Stale", ["reason"] = "Stale observation", ["revisionToken"] = document.RevisionToken }), 409);
        Assert.Equal("2", (await TestGit.RunAsync(fixture.VaultPath, "rev-list", "--count", "HEAD")).Trim());
    }

    [Fact]
    public async Task Skill_discovery_and_unified_search_filter_capabilities_and_match_http()
    {
        SeedRegistry();
        File.WriteAllText(Path.Combine(fixture.VaultPath, "deploy.md"), "# Application deployment\nVerify production releases.");
        using var http = fixture.Factory.Agent("codex");
        using var adminHttp = fixture.Factory.Agent("willem-de-jong");
        (await adminHttp.PostAsync("/api/knowledge/scan", null)).EnsureSuccessStatusCode();
        (await adminHttp.PostAsync("/api/registry/scan", null)).EnsureSuccessStatusCode();
        await using var codex = await Connect(http);
        await using var admin = await Connect(adminHttp);
        var skills = Read<Brain.Mcp.SkillSearchResponse>(await codex.CallToolAsync("brain_find_skills", new Dictionary<string, object?> { ["query"] = "deployment" }));
        Assert.Equal("plan-deployment", Assert.Single(skills.Skills).Id);
        var skill = Read<SkillView>(await codex.CallToolAsync("brain_get_skill", new Dictionary<string, object?> { ["skillId"] = "plan-deployment" }));
        Assert.Contains("# Plan", skill.Instructions);
        AssertError(await codex.CallToolAsync("brain_get_skill", new Dictionary<string, object?> { ["skillId"] = "deploy-service" }), 404);
        var capabilities = Read<CapabilitySearchResult>(await codex.CallToolAsync("brain_find_capabilities", new Dictionary<string, object?> { ["query"] = "deploy an application" }));
        Assert.Empty(capabilities.Tools);
        Assert.DoesNotContain(capabilities.Skills, s => s.Id == "deploy-service");
        var authorized = Read<CapabilitySearchResult>(await admin.CallToolAsync("brain_find_capabilities", new Dictionary<string, object?> { ["query"] = "deploy an application" }));
        Assert.Equal("coolify", Assert.Single(authorized.Tools).Id);
        Read<MemoryView>(await codex.CallToolAsync("brain_remember", new Dictionary<string, object?> { ["subject"] = "Deployment", ["content"] = "Verify deployment releases." }));
        var result = Read<BrainSearchResult>(await codex.CallToolAsync("brain_search", new Dictionary<string, object?> { ["query"] = "deployment" }));
        Assert.Single(result.Knowledge);
        Assert.Single(result.Memories);
        Assert.Equal("plan-deployment", Assert.Single(result.Skills).Id);
        Assert.Empty(result.Tools);
        Assert.Empty(result.Failures);
        var response = await http.PostAsJsonAsync("/api/search", new BrainSearchRequest("deployment"));
        response.EnsureSuccessStatusCode();
        var viaHttp = (await response.Content.ReadFromJsonAsync<BrainSearchResult>(Json))!;
        Assert.Equal(result.Memories.Select(m => m.Id), viaHttp.Memories.Select(m => m.Id));
        Assert.Equal(result.Skills.Select(s => s.Id), viaHttp.Skills.Select(s => s.Id));
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<BrainDbContext>().AuditEvents.AnyAsync(a => a.AgentId == "codex" && a.Action == "brain.capability.denied"));
    }

    [Fact]
    public async Task Revoked_permissions_and_disabled_identity_apply_without_reconnecting_the_client()
    {
        using var http = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await using var mcp = await Connect(http);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.MemoryRead]))).EnsureSuccessStatusCode();
        AssertError(await mcp.CallToolAsync("brain_remember", new Dictionary<string, object?> { ["subject"] = "Denied", ["content"] = "Do not persist." }), 403);
        AssertError(await mcp.CallToolAsync("brain_get_skill", new Dictionary<string, object?> { ["skillId"] = "plan-deployment" }), 403);
        // A missing optional module cannot break a caller who has no permission to search it.
        Directory.Delete(fixture.RegistryPath, true);
        Directory.CreateDirectory(fixture.RegistryPath);
        var result = Read<BrainSearchResult>(await mcp.CallToolAsync("brain_search", new Dictionary<string, object?> { ["query"] = "anything" }));
        Assert.Empty(result.Failures);
        Assert.Empty(result.Skills);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
            Assert.Equal(0, await db.Memories.CountAsync());
            Assert.True(await db.AuditEvents.AnyAsync(a => a.AgentId == "codex" && a.Action == "brain.authorization.denied" && a.Resource == "brain_remember"));
        }
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [], false))).EnsureSuccessStatusCode();
        using var request = Rpc("tools/list", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Unified_search_reports_unavailable_registry_but_keeps_authorized_memory_results()
    {
        using var http = fixture.Factory.Agent("codex");
        await using var mcp = await Connect(http);
        Read<MemoryView>(await mcp.CallToolAsync("brain_remember", new Dictionary<string, object?> { ["subject"] = "Keep", ["content"] = "Keep memory searchable." }));
        Directory.Delete(Path.Combine(fixture.RegistryPath, "tools"));
        var result = Read<BrainSearchResult>(await mcp.CallToolAsync("brain_search", new Dictionary<string, object?> { ["query"] = "searchable" }));
        Assert.Single(result.Memories);
        Assert.Equal("registry", Assert.Single(result.Failures).Source);
        Assert.Equal(503, result.Failures[0].Status);
        Assert.Empty(result.Skills);
        Assert.Empty(result.Tools);
    }

    [Fact]
    public async Task Authentication_origin_and_stateless_transport_are_enforced()
    {
        using var anonymous = fixture.Factory.CreateClient();
        using (var request = Rpc("tools/list", new { }))
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(request)).StatusCode);
        using var http = fixture.Factory.Agent("codex");
        using (var request = Rpc("tools/list", new { }))
        {
            request.Headers.Add("Origin", "https://untrusted.example");
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(request)).StatusCode);
        }
        using (var request = Rpc("tools/list", new { }))
        {
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Assert.False(response.Headers.Contains("Mcp-Session-Id"));
        }
        http.DefaultRequestHeaders.Remove("X-Agent-Id");
        http.DefaultRequestHeaders.Add("X-Agent-Id", "willem-de-jong");
        using (var request = Rpc("tools/list", new { }))
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("brain_remember", "{\"subject\":\"Bad\",\"content\":\"No write\",\"agentId\":\"willem-de-jong\"}")]
    [InlineData("brain_propose_knowledge_change", "{\"path\":\"new.md\",\"operation\":\"create_document\",\"content\":\"No write\",\"reason\":\"test\",\"approved\":true}")]
    [InlineData("brain_remember", "{\"subject\":\"Bad\",\"content\":\"No write\",\"memoryType\":999}")]
    [InlineData("brain_remember", "{\"subject\":\"Bad\",\"content\":\"No write\",\"visibility\":\"unknown\"}")]
    [InlineData("brain_remember", "{\"subject\":\"Bad\",\"content\":\"No write\",\"importance\":2}")]
    [InlineData("brain_read_document", "{\"path\":\"../outside.md\"}")]
    [InlineData("brain_find_skills", "{\"query\":\"deploy\",\"limit\":0}")]
    [InlineData("brain_propose_knowledge_change", "{\"path\":\"new.md\",\"operation\":999,\"content\":\"No write\",\"reason\":\"test\"}")]
    public async Task Invalid_arguments_cannot_impersonate_agents_bypass_review_or_persist(string name, string json)
    {
        using var http = fixture.Factory.Agent("codex");
        await using var mcp = await Connect(http);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        try { Assert.True((await mcp.CallToolAsync(name, arguments)).IsError); }
        catch (McpProtocolException e) { Assert.Equal(McpErrorCode.InvalidParams, e.ErrorCode); }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(0, await db.Memories.CountAsync());
        Assert.Equal(0, await db.KnowledgeWriteProposals.CountAsync());
    }

    private static async Task<McpClient> Connect(HttpClient http, string? version = null) =>
        await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp,
            EnableStandaloneGetStream = false
        }, http, ownsHttpClient: false), new() { ProtocolVersion = version, ClientInfo = new() { Name = "brain-integration-tests", Version = "1.0" } });

    private static T Read<T>(CallToolResult result)
    {
        Assert.False(result.IsError is true, string.Join('\n', result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        Assert.NotNull(result.StructuredContent);
        Assert.NotEmpty(result.Content.OfType<TextContentBlock>());
        return JsonSerializer.SerializeToElement(result.StructuredContent).Deserialize<T>(Json)!;
    }
    private static void AssertError(CallToolResult result, int status)
    {
        Assert.True(result.IsError);
        Assert.Contains($"Brain {status}:", string.Join('\n', result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
    }
    private static HttpRequestMessage Rpc(string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        request.Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params = parameters }), Encoding.UTF8, "application/json");
        return request;
    }
    private void SeedRegistry()
    {
        Write("tools/coolify/manifest.yaml", "id: coolify\nname: Coolify\ndescription: Deploy an application\nprovider: mcp\npermissions: [deployment.write]\ncapabilities: [deploy_service]");
        Write("skills/deploy-service/manifest.yaml", "id: deploy-service\nname: Deploy Service\ndescription: Deploy an application\nrequired_tools: [coolify]\nrequired_permissions: [deployment.production]");
        Write("skills/deploy-service/SKILL.md", "# Deploy\nReview the release.");
        Write("skills/plan-deployment/manifest.yaml", "id: plan-deployment\nname: Plan Deployment\ndescription: Plan an application deployment\nrequired_tools: []\nrequired_permissions: [knowledge.read]");
        Write("skills/plan-deployment/SKILL.md", "# Plan\nRead the deployment procedure.");
    }
    private void Write(string relative, string content)
    {
        var path = Path.Combine(fixture.RegistryPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
