using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class MemoryApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false) } };
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Identity_requires_bound_credential_not_an_agent_id_header()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/agents/me")).StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-Agent-Id", "willem-de-jong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/agents/me")).StatusCode);
        using var impostor = fixture.Factory.Agent("codex");
        impostor.DefaultRequestHeaders.Remove("X-Agent-Id");
        impostor.DefaultRequestHeaders.Add("X-Agent-Id", "willem-de-jong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await impostor.GetAsync("/api/agents/me")).StatusCode);
        using var legitimate = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.OK, (await legitimate.GetAsync("/api/agents/me")).StatusCode);
    }

    [Fact]
    public async Task Remember_read_and_vector_search_round_trip_preserves_natural_language_and_metadata()
    {
        using var client = fixture.Factory.Agent("codex");
        var memory = await Remember(client, new(MemoryType.Incident, "Proxmox cluster", "Corosync configuration is missing on nacht-02.",
            Importance: 0.9, Confidence: 0.95, Source: "troubleshooting", Metadata: JsonSerializer.SerializeToElement(new { host = "nacht-02" })));
        await Remember(client, new(MemoryType.Preference, "Food", "Chocolate cookies with coffee."));
        var read = await client.GetFromJsonAsync<MemoryView>($"/api/memories/{memory.Id}", Json);
        Assert.Equal(memory.Content, read!.Content);
        Assert.Equal("nacht-02", read.Metadata.GetProperty("host").GetString());
        Assert.Equal(1, read.ReferenceCount);
        var results = await Search(client, new("Proxmox corosync cluster", Limit: 1));
        Assert.Equal(memory.Id, Assert.Single(results).Id);
        Assert.InRange(results[0].Score, 0.1, 1);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        var stored = await db.MemoryEmbeddings.SingleAsync(e => e.MemoryId == memory.Id);
        Assert.Equal(384, stored.Embedding.ToArray().Length);
        Assert.Equal("token-hash-v1", stored.Model);
        Assert.Equal(2, await db.AuditEvents.CountAsync(e => e.Action == "brain.memory.created"));
    }

    [Fact]
    public async Task Visibility_filters_apply_before_limit_and_agent_filter_never_grants_access()
    {
        using var owner = fixture.Factory.Agent("willem-de-jong");
        using var teammate = fixture.Factory.Agent("codex");
        await CreateOther(owner, "different-team", ["agent"], []);
        using var other = fixture.Factory.Agent("other");
        var privateMemory = await Remember(owner, new(MemoryType.Fact, "cluster", "cluster"));
        var teamMemory = await Remember(owner, new(MemoryType.Fact, "team", "cluster corosync", MemoryVisibility.Team));
        var orgMemory = await Remember(owner, new(MemoryType.Fact, "organization", "cluster homelab", MemoryVisibility.Organization));
        Assert.Equal(HttpStatusCode.NotFound, (await teammate.GetAsync($"/api/memories/{privateMemory.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/memories/{teamMemory.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teammate.GetAsync($"/api/memories/{teamMemory.Id}")).StatusCode);
        var team = await Search(teammate, new("cluster", AgentId: "willem-de-jong"));
        Assert.Equal(2, team.Length);
        Assert.DoesNotContain(team, m => m.Id == privateMemory.Id);
        var outsiders = await Search(other, new("cluster", AgentId: "willem-de-jong", Limit: 1));
        Assert.Equal(orgMemory.Id, Assert.Single(outsiders).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await teammate.DeleteAsync($"/api/memories/{orgMemory.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/memories/{privateMemory.Id}")).StatusCode);
    }

    [Fact]
    public async Task Roles_direct_grants_and_revocation_are_resolved_from_database_each_request()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        await CreateOther(admin, null, [], [Permissions.MemoryRead]);
        using var other = fixture.Factory.Agent("other");
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/memories/search", new MemorySearchRequest("cluster"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/memories", new RememberRequest(MemoryType.Fact, "test", "content"), Json)).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/other/access", new UpdateAccessRequest(["agent"], []))).EnsureSuccessStatusCode();
        await Remember(other, new(MemoryType.Fact, "test", "content"));
        (await admin.PutAsJsonAsync("/api/agents/other/access", new UpdateAccessRequest([], []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/memories/search", new MemorySearchRequest("cluster"))).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/other/access", new UpdateAccessRequest([], [], false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/agents/me")).StatusCode);
    }

    [Fact]
    public async Task Metadata_filters_and_embedding_model_isolation_are_applied_in_postgres()
    {
        using var client = fixture.Factory.Agent("codex");
        var match = await Remember(client, new(MemoryType.Incident, "Nacht-02 cluster", "Corosync failure", Importance: 0.9, Confidence: 0.8));
        await Remember(client, new(MemoryType.Fact, "Nacht-00", "Cluster configuration", Importance: 0.2));
        var found = await Search(client, new("cluster", "codex", MemoryType.Incident, "NACHT", 0.8, 0.7));
        Assert.Equal(match.Id, Assert.Single(found).Id);
        Assert.Empty(await Search(client, new("cluster", MinimumConfidence: 0.99)));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.MemoryEmbeddings.Where(e => e.MemoryId == match.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Model, "other-model"));
        Assert.Empty(await Search(client, new("cluster", MemoryType: MemoryType.Incident)));
    }

    [Fact]
    public async Task Expired_memories_are_hidden_before_worker_cleanup_and_embeddings_cascade_on_delete()
    {
        using var client = fixture.Factory.Agent("codex");
        var expired = await Remember(client, new(MemoryType.TaskContext, "temporary", "temporary task"));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.Memories.Where(m => m.Id == expired.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/memories/{expired.Id}")).StatusCode);
        Assert.Empty(await Search(client, new("temporary")));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IMemoryStore>().DeleteExpiredAsync(DateTimeOffset.UtcNow, default));
        Assert.Equal(0, await db.MemoryEmbeddings.CountAsync());
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "brain.memory.expired"));
        var kept = await Remember(client, new(MemoryType.Fact, "kept", "persistent"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/memories/{kept.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/memories/{kept.Id}")).StatusCode);
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "brain.memory.deleted"));
    }

    [Fact]
    public async Task Administrative_access_is_denied_and_audited_and_openapi_is_authenticated()
    {
        using var agent = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/audit")).StatusCode);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var audit = await admin.GetFromJsonAsync<AuditEvent[]>("/api/audit");
        Assert.Contains(audit!, a => a.AgentId == "codex" && a.Action == "brain.authorization.denied");
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/openapi/v1.json")).StatusCode);
        var openapi = await agent.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(openapi.GetProperty("paths").TryGetProperty("/api/memories/search", out _));
        Assert.Equal(2, openapi.GetProperty("security")[0].EnumerateObject().Count());
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_and_unknown_permissions_cannot_be_persisted()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Other", Permissions: ["unknown.superuser"]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/memories", new { memoryType = "fact", subject = "test", content = "content", visibility = "global" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/memories", new { memoryType = "fact", subject = "test", content = "content", metadata = "not-an-object" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/memories", new { memoryType = "fact", subject = "test", content = "content", agentId = "codex" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/memories", new RememberRequest(MemoryType.Fact, "test", "content", ExpiresAt: DateTimeOffset.UtcNow.AddDays(-1)), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/memories/search", new MemorySearchRequest("cluster", Limit: 101))).StatusCode);
    }

    [Fact]
    public async Task Seed_is_idempotent_and_migration_has_no_model_drift()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await DatabaseInitializer.InitializeAsync(db, true);
        Assert.Equal(3, await db.Agents.CountAsync());
        Assert.Equal(Permissions.All.Length, await db.Permissions.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Failed_vector_persistence_rolls_back_memory_and_audit()
    {
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
            await Assert.ThrowsAsync<DbUpdateException>(() => store.CreateAsync(new AgentMemory
            {
                AgentId = "codex", Subject = "invalid vector", Content = "Must not persist", CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }, [1f, 0f], new("test", "invalid-dimensions", 2), new AuditEvent
            { AgentId = "codex", Action = "must.not.persist", Resource = "test", Timestamp = DateTimeOffset.UtcNow }, default));
        }
        using var verification = fixture.Factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(0, await db.Memories.CountAsync());
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    private static async Task<MemoryView> Remember(HttpClient client, RememberRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/memories", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MemoryView>(Json))!;
    }
    private static async Task<MemorySearchResult[]> Search(HttpClient client, MemorySearchRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/memories/search", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemorySearchResult[]>(Json))!;
    }
    private static async Task CreateOther(HttpClient admin, string? teamId, string[] roles, string[] permissions)
    {
        var response = await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Other agent", TeamId: teamId, Roles: roles, Permissions: permissions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
