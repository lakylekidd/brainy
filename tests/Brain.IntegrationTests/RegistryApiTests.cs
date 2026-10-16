using System.Net;
using System.Net.Http.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class RegistryApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Discovery_ranks_allowed_skills_and_includes_every_related_allowed_tool()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(4, (await Scan(admin)).Indexed);
        var production = await Capabilities(admin, new("deploy an application to production", ["production"], 1));
        Assert.Equal("deploy-service", Assert.Single(production.Skills).Id);
        var tool = Assert.Single(production.Tools);
        Assert.Equal("coolify", tool.Id); // Dependency has no production tag; still included.
        Assert.Equal("mcp", tool.Metadata.Provider);
        Assert.Contains("deploy_service", tool.Metadata.Capabilities);
        var skill = await admin.GetFromJsonAsync<SkillView>("/api/skills/deploy-service");
        Assert.Equal("# Deploy\nUse the release procedure.", skill!.Instructions);
        Assert.StartsWith("skills/", skill.Skill.Source);
        using var agent = fixture.Factory.Agent("codex");
        var allowed = await Capabilities(agent, new("deploy an application to production", Limit: 1));
        Assert.Equal("plan-deployment", Assert.Single(allowed.Skills).Id);
        Assert.Empty(allowed.Tools);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/skills/deploy-service")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/tools/coolify")).StatusCode);
        Assert.Equal("plan-deployment", Assert.Single((await agent.GetFromJsonAsync<RegistryResult[]>("/api/skills"))!).Id);
        Assert.Empty((await agent.GetFromJsonAsync<RegistryResult[]>("/api/tools"))!);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.AgentId == "codex" && a.Action == "brain.capability.denied" && a.Resource == "tool:coolify"));
        var discoveries = await db.AuditEvents.Where(a => a.AgentId == "codex" && a.Action == "brain.capability.discovered").ToListAsync();
        Assert.Contains(discoveries, a => a.MetadataJson.Contains("plan-deployment", StringComparison.Ordinal));
        Assert.Equal(384, (await db.RegistryEntries.FirstAsync()).Embedding.ToArray().Length);
        Assert.Equal("deploy-service", Assert.Single(await Search(admin, "skills", new("deploy an application to production", Limit: 1))).Id);
        Assert.Equal("coolify", Assert.Single(await Search(admin, "tools", new("deploy application", Limit: 1))).Id);
    }

    [Fact]
    public async Task Role_and_direct_grants_are_unioned_and_tool_revocations_take_effect_on_next_request()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
            db.Roles.Add(new Role { Id = "production-planner", Permissions = await db.Permissions.Where(p =>
                p.Id == Permissions.RegistryRead || p.Id == "deployment.production" || p.Id == "deployment.read").ToListAsync() });
            await db.SaveChangesAsync();
        }
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest(["production-planner"], ["deployment.write"]))).EnsureSuccessStatusCode();
        using var agent = fixture.Factory.Agent("codex");
        Assert.Contains((await Capabilities(agent, new("deployment"))).Skills, s => s.Id == "deploy-service");
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest(["production-planner"], []))).EnsureSuccessStatusCode();
        var denied = await Capabilities(agent, new("deployment"));
        Assert.Empty(denied.Tools);
        Assert.DoesNotContain(denied.Skills, s => s.Id == "deploy-service");
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/skills/deploy-service")).StatusCode);
    }

    [Fact]
    public async Task Missing_tools_and_live_permission_changes_deny_skills_before_reindexing()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        using var agent = fixture.Factory.Agent("codex");
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest(["agent"], ["deployment.production", "deployment.read", "deployment.write"]))).EnsureSuccessStatusCode();
        var manifest = Path.Combine(fixture.RegistryPath, "tools/coolify/manifest.yaml");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("deployment.write", "secrets.production.read"));
        Assert.DoesNotContain((await Capabilities(agent, new("deploy"))).Skills, s => s.Id == "deploy-service");
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/skills/deploy-service")).StatusCode);
        // Even the administrator cannot search a skill with a stale dependency until reindexing.
        Assert.DoesNotContain((await Capabilities(admin, new("deploy"))).Skills, s => s.Id == "deploy-service");
        await Scan(admin);
        Assert.Contains((await Capabilities(admin, new("deploy"))).Skills, s => s.Id == "deploy-service");
        Directory.Delete(Path.Combine(fixture.RegistryPath, "tools/coolify"), true);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/skills/deploy-service")).StatusCode);
        Assert.DoesNotContain((await Capabilities(admin, new("deploy"))).Skills, s => s.Id == "deploy-service");
        Assert.Equal(1, (await Scan(admin)).Removed);
    }

    [Fact]
    public async Task Source_changes_instructions_deletions_and_model_changes_require_fresh_index()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        var skipped = await Scan(admin);
        Assert.Equal(0, skipped.Indexed);
        Assert.Equal(4, skipped.Unchanged);
        File.AppendAllText(Path.Combine(fixture.RegistryPath, "skills/plan-deployment/SKILL.md"), "\nNew instruction.");
        using var agent = fixture.Factory.Agent("codex");
        Assert.Empty(await Search(agent, "skills", new("deployment")));
        Assert.Contains("New instruction", (await agent.GetFromJsonAsync<SkillView>("/api/skills/plan-deployment"))!.Instructions);
        await Scan(admin);
        Assert.Single(await Search(agent, "skills", new("deployment")));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.RegistryEntries.ExecuteUpdateAsync(s => s.SetProperty(e => e.EmbeddingModel, "obsolete-model"));
        Assert.Empty(await Search(admin, "skills", new("deployment")));
        Assert.Equal(4, (await Scan(admin)).Indexed);
        Directory.Delete(Path.Combine(fixture.RegistryPath, "skills/plan-deployment"), true);
        Assert.Empty(await Search(agent, "skills", new("deployment")));
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/skills/plan-deployment")).StatusCode);
        Assert.Equal(1, (await Scan(admin)).Removed);
        Assert.Equal(3, await db.RegistryEntries.CountAsync());
    }

    [Fact]
    public async Task Invalid_source_fails_closed_and_preserves_previous_index_atomically()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        var manifest = Path.Combine(fixture.RegistryPath, "tools/coolify/manifest.yaml");
        var original = File.ReadAllText(manifest);
        File.AppendAllText(manifest, "\npermissions: []");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync("/api/registry/scan", null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync("/api/skills")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(4, await db.RegistryEntries.CountAsync());
        File.WriteAllText(manifest, original);
        Assert.Equal(4, (await Scan(admin)).Unchanged);
        Assert.NotEmpty((await Capabilities(admin, new("deploy"))).Skills);
        // A failed replacement rolls back deletion and inserts together.
        var index = scope.ServiceProvider.GetRequiredService<IRegistryIndex>();
        var definition = (await scope.ServiceProvider.GetRequiredService<IRegistrySource>().ReadAsync(default))[0];
        var provider = new DevelopmentEmbeddingProvider();
        var embedded = new EmbeddedRegistryDefinition(definition, await provider.EmbedAsync("deployment", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => index.ReplaceAsync([embedded, embedded], provider.Model,
            DateTimeOffset.UtcNow, new AuditEvent { AgentId = "test", Action = "test", Resource = "registry" }, default));
        using var verify = fixture.Factory.Services.CreateScope();
        Assert.Equal(4, await verify.ServiceProvider.GetRequiredService<BrainDbContext>().RegistryEntries.CountAsync());
    }

    [Fact]
    public async Task Concurrent_scan_is_excluded_and_a_source_changed_during_embedding_is_not_published()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var scope = fixture.Factory.Services.CreateScope();
        var index = scope.ServiceProvider.GetRequiredService<IRegistryIndex>();
        await using (var lease = await index.TryAcquireScanAsync(default))
        {
            Assert.NotNull(lease);
            Assert.True((await Scan(admin)).AlreadyRunning);
        }
        var service = new RegistryService(scope.ServiceProvider.GetRequiredService<IRegistrySource>(), index,
            new MutatingProvider(() => File.AppendAllText(Path.Combine(fixture.RegistryPath, "skills/plan-deployment/SKILL.md"), "\nChanged.")),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(), TimeProvider.System);
        Assert.Equal(409, (await Assert.ThrowsAsync<BrainException>(() => service.ScanCoreAsync("brain-worker"))).StatusCode);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().RegistryEntries.CountAsync());
        Assert.False((await Scan(admin)).AlreadyRunning);
    }

    [Fact]
    public async Task Manifest_changed_while_query_embedding_is_generated_is_not_returned()
    {
        Seed();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await Scan(admin);
        using var scope = fixture.Factory.Services.CreateScope();
        var path = Path.Combine(fixture.RegistryPath, "tools/coolify/manifest.yaml");
        var service = new RegistryService(scope.ServiceProvider.GetRequiredService<IRegistrySource>(),
            scope.ServiceProvider.GetRequiredService<IRegistryIndex>(),
            new MutatingProvider(() => File.WriteAllText(path, File.ReadAllText(path).Replace("deployment.write", "unknown.permission"))),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(), TimeProvider.System);
        var actor = (await scope.ServiceProvider.GetRequiredService<IAgentDirectory>().ResolveAsync("willem-de-jong", default))!;
        var result = await service.FindCapabilitiesAsync(actor, new("deployment"));
        Assert.DoesNotContain(result.Skills, s => s.Id == "deploy-service");
        Assert.Empty(result.Tools);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("nan")]
    [InlineData("dimension")]
    public async Task Invalid_embedding_provider_output_does_not_publish_a_partial_index(string mode)
    {
        Seed();
        using var scope = fixture.Factory.Services.CreateScope();
        var service = new RegistryService(scope.ServiceProvider.GetRequiredService<IRegistrySource>(),
            scope.ServiceProvider.GetRequiredService<IRegistryIndex>(), new InvalidProvider(mode),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ScanCoreAsync("brain-worker"));
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().RegistryEntries.CountAsync());
    }

    [Fact]
    public async Task Authentication_operation_permissions_request_validation_and_openapi_cover_registry_routes()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/skills")).StatusCode);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var agent = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsync("/api/registry/scan", null)).StatusCode);
        foreach (var request in new[] { new RegistrySearchRequest(""), new("deploy", Limit: 0), new("deploy", Limit: 101), new("deploy", [""]) })
            Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/capabilities/search", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/skills/search", new { query = "deploy", agentId = "willem-de-jong" })).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.MemoryRead]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/skills")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/tools/coolify")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/capabilities/search", new RegistrySearchRequest("deploy"))).StatusCode);
        var openapi = await admin.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/capabilities/search", openapi);
        Assert.Contains("/api/skills/{id}", openapi);
        Assert.Contains("/api/tools/search", openapi);
    }

    private void Seed()
    {
        Write("tools/coolify/manifest.yaml", "id: coolify\nname: Coolify\ndescription: Deploy application releases\nprovider: mcp\npermissions: [deployment.read, deployment.write]\ncapabilities: [deploy_service]");
        Write("skills/deploy-service/manifest.yaml", "id: deploy-service\nname: Deploy Service\ndescription: Deploy an application to production\ntags: [production]\nrequired_tools: [coolify]\nrequired_permissions: [deployment.production]");
        Write("skills/deploy-service/SKILL.md", "# Deploy\nUse the release procedure.");
        Write("skills/plan-deployment/manifest.yaml", "id: plan-deployment\nname: Plan Deployment\ndescription: Plan deployment releases\ntags: [planning]\nrequired_tools: []\nrequired_permissions: [knowledge.read]");
        Write("skills/plan-deployment/SKILL.md", "# Plan\nRead canonical procedures.");
        Write("skills/blocked/manifest.yaml", "id: blocked\nname: Blocked\ndescription: Deploy application production\nrequired_tools: [unconfigured-tool]\nrequired_permissions: []");
        Write("skills/blocked/SKILL.md", "# Missing dependency");
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(fixture.RegistryPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
    private static async Task<RegistryScanResult> Scan(HttpClient client)
    {
        var response = await client.PostAsync("/api/registry/scan", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegistryScanResult>())!;
    }
    private static async Task<RegistryResult[]> Search(HttpClient client, string kind, RegistrySearchRequest request)
    {
        var response = await client.PostAsJsonAsync($"/api/{kind}/search", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegistryResult[]>())!;
    }
    private static async Task<CapabilitySearchResult> Capabilities(HttpClient client, RegistrySearchRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/capabilities/search", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CapabilitySearchResult>())!;
    }
    private sealed class MutatingProvider(Action mutate) : IEmbeddingProvider
    {
        private readonly DevelopmentEmbeddingProvider inner = new();
        private bool mutated;
        public EmbeddingModel Model => inner.Model;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        {
            if (!mutated) { mutate(); mutated = true; }
            return inner.EmbedAsync(text, ct);
        }
    }
    private sealed class InvalidProvider(string mode) : IEmbeddingProvider
    {
        public EmbeddingModel Model => new("test", "invalid", mode == "dimension" ? 1536 : 384);
        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        {
            var vector = new float[mode == "empty" ? 0 : 384];
            if (mode is "nan" or "dimension") vector[0] = mode == "nan" ? float.NaN : 1;
            return Task.FromResult(vector);
        }
    }
}
