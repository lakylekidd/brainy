using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class GraphApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Global_search_details_and_expansion_derive_stable_source_references_without_bodies_or_arbitrary_metadata()
    {
        await SeedAsync();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var graph = await Get(admin, "/api/graph?limit=200");
        Assert.All(GraphIds.Types, type => Assert.Contains(graph.Entities, node => node.Type == type));
        Assert.Contains(graph.Relationships, edge => edge.Type == "requires_tool" && edge.Source == "skill:deploy" && edge.Target == "tool:production");
        Assert.Contains(graph.Relationships, edge => edge.Type == "requires_permission" && edge.Source == "tool:production" && edge.Target == "permission:deployment.production");
        Assert.Contains(graph.Relationships, edge => edge.Type == "has_role" && edge.Source == "agent:codex" && edge.Target == "role:agent");
        Assert.Contains(graph.Relationships, edge => edge.Type == "grants_permission" && edge.Source == "role:agent" && edge.Target == "permission:knowledge.read");
        Assert.Contains(graph.Relationships, edge => edge.Type == "grants_permission" && edge.Source == "agent:metadata-enrichment-worker" && edge.Target == "permission:knowledge.read");
        Assert.Contains(graph.Relationships, edge => edge.Type == "references" && edge.Source == "document:root.md" && edge.Target == "document:middle.md");
        var detail = await admin.GetFromJsonAsync<GraphEntity>("/api/graph/entity?id=document:root.md");
        Assert.Equal(new GraphReference("knowledge_document", "root.md"), detail!.Source);
        Assert.Equal("Root", detail.Label);
        Assert.Equal("root.md", detail.Metadata["path"]);
        foreach (var path in new[] { "/api/graph?limit=200", "/api/graph/entity?id=skill:deploy", "/api/graph/neighbors?id=document:root.md&depth=2&limit=200" })
            Assert.DoesNotContain("DO_NOT_RENDER", await admin.GetStringAsync(path));
        var search = await Get(admin, "/api/graph/search?query=production&types=tool&limit=1");
        Assert.Equal("tool:production", Assert.Single(search.Entities).Id);
        var relationships = graph.Relationships.ToDictionary(edge => edge.Id);
        var again = await Get(admin, "/api/graph?limit=200");
        Assert.All(again.Relationships, edge => Assert.Equal(relationships[edge.Id], edge));
        Assert.All(graph.Relationships, edge => AssertEndpoints(graph, edge));
    }

    [Fact]
    public async Task Every_surface_filters_hidden_entities_dependencies_permissions_and_metadata_before_limits()
    {
        await SeedAsync();
        using var caller = fixture.Factory.Agent("codex");
        var graph = await Get(caller, "/api/graph?limit=200");
        Assert.DoesNotContain(graph.Entities, node => node.Type is "agent" or "role" or "permission");
        Assert.DoesNotContain(graph.Entities, node => node.Id is "tool:production" or "skill:deploy");
        var search = await Get(caller, "/api/graph/search?query=plan&types=skill&limit=1");
        Assert.Equal("skill:plan", Assert.Single(search.Entities).Id);
        Assert.Empty((await Get(caller, "/api/graph/search?query=production")).Entities);
        var neighbors = await Get(caller, "/api/graph/neighbors?id=document:root.md&depth=2&limit=200");
        Assert.DoesNotContain(neighbors.Entities, node => node.Id is "tool:production" or "skill:deploy" || node.Type is "agent" or "permission");
        Assert.All(neighbors.Relationships, edge => AssertEndpoints(neighbors, edge));
        foreach (var id in new[] { "tool:production", "skill:deploy", "agent:codex", "role:administrator", "permission:deployment.production", "tool:missing" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync("/api/graph/entity?id=" + Uri.EscapeDataString(id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync("/api/graph/neighbors?id=" + Uri.EscapeDataString(id))).StatusCode);
        }
        var raw = await caller.GetStringAsync("/api/graph?limit=200");
        Assert.DoesNotContain("deployment.production", raw);
        Assert.DoesNotContain("DO_NOT_RENDER", raw);
    }

    [Fact]
    public async Task Depth_zero_one_two_and_filters_are_enforced_on_the_server()
    {
        await SeedAsync();
        using var caller = fixture.Factory.Agent("codex");
        var zero = await Get(caller, "/api/graph/neighbors?id=document:root.md&types=document&depth=0");
        Assert.Equal("document:root.md", Assert.Single(zero.Entities).Id);
        Assert.Empty(zero.Relationships);
        var one = await Get(caller, "/api/graph/neighbors?id=document:root.md&types=document&depth=1");
        Assert.Equal(2, one.Entities.Count);
        Assert.DoesNotContain(one.Entities, node => node.Id == "document:end.md");
        var two = await Get(caller, "/api/graph/neighbors?id=document:root.md&types=document&depth=2");
        Assert.Equal(3, two.Entities.Count);
        Assert.Equal(2, two.Relationships.Count);
        Assert.All(two.Entities, node => Assert.Equal("document", node.Type));
        var reverse = await Get(caller, "/api/graph/neighbors?id=document:end.md&types=document&depth=1");
        Assert.Contains(reverse.Entities, node => node.Id == "document:middle.md");
        Assert.DoesNotContain(reverse.Entities, node => node.Id == "document:root.md");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var permissions = await Get(admin, "/api/graph/neighbors?id=permission:deployment.production&depth=1");
        Assert.Contains(permissions.Entities, node => node.Id == "tool:production");
        Assert.Contains(permissions.Entities, node => node.Id == "role:administrator");
        Assert.All(permissions.Relationships, edge => AssertEndpoints(permissions, edge));
    }

    [Theory]
    [InlineData("/api/graph?limit=201")]
    [InlineData("/api/graph?limit=0")]
    [InlineData("/api/graph?types=unknown")]
    [InlineData("/api/graph?types=")]
    [InlineData("/api/graph/neighbors?id=document:root.md&depth=3")]
    [InlineData("/api/graph/neighbors?id=document:root.md&depth=-1")]
    [InlineData("/api/graph/neighbors?id=document:root.md&limit=201")]
    [InlineData("/api/graph/entity?id=document:../outside.md")]
    public async Task Invalid_bounds_filters_and_ids_are_rejected(string path)
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Authentication_source_grants_and_live_revocation_apply_to_all_graph_operations()
    {
        await SeedAsync();
        using var anonymous = fixture.Factory.CreateClient();
        foreach (var path in new[] { "/api/graph", "/api/graph/search?query=plan", "/api/graph/entity?id=skill:plan", "/api/graph/neighbors?id=skill:plan" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var caller = fixture.Factory.Agent("codex");
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.RegistryRead, "deployment.production"]))).EnsureSuccessStatusCode();
        Assert.Equal("tool:production", (await caller.GetFromJsonAsync<GraphEntity>("/api/graph/entity?id=tool:production"))!.Id);
        File.WriteAllText(Path.Combine(fixture.RegistryPath, "tools", "production", "manifest.yaml"), ToolManifest("secrets.production.read"));
        Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync("/api/graph/entity?id=tool:production")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync("/api/graph/entity?id=skill:deploy")).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], []))).EnsureSuccessStatusCode();
        foreach (var path in new[] { "/api/graph", "/api/graph/search?query=plan", "/api/graph/entity?id=skill:plan", "/api/graph/neighbors?id=skill:plan" })
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.GetAsync(path)).StatusCode);
        using var ui = fixture.Factory.CreateClient();
        ui.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BrainFactory.UiAdminToken);
        (await ui.GetAsync("/api/graph")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Canonical_changes_deletions_and_reindexing_update_graph_references_without_a_second_graph_store()
    {
        await SeedAsync();
        using var admin = fixture.Factory.Agent("willem-de-jong");
        File.WriteAllText(Path.Combine(fixture.VaultPath, "root.md"), "---\ngraph_references: [document:end.md]\n---\n# Changed\nDO_NOT_RENDER");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/graph/entity?id=document:root.md")).StatusCode);
        Assert.DoesNotContain((await Get(admin, "/api/graph")).Entities, node => node.Id == "document:root.md");
        (await admin.PostAsync("/api/knowledge/scan", null)).EnsureSuccessStatusCode();
        var graph = await Get(admin, "/api/graph/neighbors?id=document:root.md&types=document");
        Assert.Equal("Changed", graph.Entities.Single(node => node.Id == "document:root.md").Label);
        Assert.Contains(graph.Relationships, edge => edge.Target == "document:end.md");
        Assert.DoesNotContain(graph.Entities, node => node.Id == "document:middle.md");
        File.Delete(Path.Combine(fixture.VaultPath, "end.md"));
        Assert.DoesNotContain((await Get(admin, "/api/graph/neighbors?id=document:root.md&types=document")).Entities,
            node => node.Id == "document:end.md");
        Directory.Delete(Path.Combine(fixture.RegistryPath, "tools", "production"), true);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/graph/entity?id=skill:deploy")).StatusCode);
    }

    [Fact]
    public async Task Large_sources_are_filtered_and_bounded_in_overview_search_and_reverse_expansion()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        var vault = scope.ServiceProvider.GetRequiredService<IKnowledgeVault>();
        for (var i = 0; i < 450; i++)
        {
            var path = $"large-{i:D4}.md";
            File.WriteAllText(Path.Combine(fixture.VaultPath, path), "# Large " + i);
            var snapshot = await vault.ReadAsync(path, default);
            db.KnowledgeDocuments.Add(new KnowledgeDocument
            {
                Path = path, Title = "Large " + i, ContentHash = snapshot.ContentHash, RevisionToken = snapshot.RevisionToken,
                IndexedAt = DateTimeOffset.UtcNow, ModifiedAt = snapshot.ModifiedAt,
                GraphReferences = i < 10
                    ? Enumerable.Range(0, 100).Where(target => target != i).Select(target => $"document:large-{target:D4}.md").ToArray()
                    : ["document:large-0000.md"],
                GraphVersion = 1
            });
        }
        await db.SaveChangesAsync();
        using var caller = fixture.Factory.Agent("codex");
        var overview = await Get(caller, "/api/graph?types=document&limit=7");
        Assert.Equal(7, overview.Entities.Count);
        Assert.True(overview.Truncated);
        var maximum = await Get(caller, "/api/graph?types=document&limit=200");
        Assert.Equal(GraphService.MaximumEntities, maximum.Entities.Count);
        Assert.Equal(GraphService.MaximumRelationships, maximum.Relationships.Count);
        Assert.True(maximum.Truncated);
        Assert.All(maximum.Relationships, edge => AssertEndpoints(maximum, edge));
        var neighbors = await Get(caller, "/api/graph/neighbors?id=document:large-0000.md&types=document&depth=2&limit=9");
        Assert.Equal(9, neighbors.Entities.Count);
        Assert.True(neighbors.Truncated);
        Assert.InRange(neighbors.Relationships.Count, 1, GraphService.MaximumRelationships);
        var search = await Get(caller, "/api/graph/search?query=large-0449.md&types=document&limit=1");
        Assert.Equal("document:large-0449.md", Assert.Single(search.Entities).Id);
        Assert.False(search.Truncated);
    }

    [Fact]
    public async Task Empty_sources_and_invalid_registry_have_explicit_results_and_do_not_disable_other_routes()
    {
        using var caller = fixture.Factory.Agent("codex");
        Assert.Empty((await Get(caller, "/api/graph")).Entities);
        var folder = Path.Combine(fixture.RegistryPath, "tools", "invalid");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "manifest.yaml"), "unknown: invalid");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await caller.GetAsync("/api/graph")).StatusCode);
        (await caller.GetAsync("/api/agents/me")).EnsureSuccessStatusCode();
        Assert.Empty((await Get(caller, "/api/graph?types=document")).Entities);
    }

    private async Task SeedAsync()
    {
        File.WriteAllText(Path.Combine(fixture.VaultPath, "root.md"),
            "---\ngraph_references: [document:middle.md, skill:deploy, agent:codex]\npassword: DO_NOT_RENDER\n---\n# Root\nDO_NOT_RENDER");
        File.WriteAllText(Path.Combine(fixture.VaultPath, "middle.md"), "---\ngraph_references: [document:end.md]\n---\n# Middle");
        File.WriteAllText(Path.Combine(fixture.VaultPath, "end.md"), "# End");
        var tool = Path.Combine(fixture.RegistryPath, "tools", "production");
        Directory.CreateDirectory(tool);
        File.WriteAllText(Path.Combine(tool, "manifest.yaml"), ToolManifest("deployment.production"));
        foreach (var (id, name, tools) in new[] { ("deploy", "Production deployment", "[production]"), ("plan", "Plan deployment", "[]") })
        {
            var folder = Path.Combine(fixture.RegistryPath, "skills", id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "manifest.yaml"),
                $"id: {id}\nname: {name}\ndescription: DO_NOT_RENDER\ntags: []\nrequired_permissions: []\nrequired_tools: {tools}");
            File.WriteAllText(Path.Combine(folder, "SKILL.md"), "# DO_NOT_RENDER\nInstruction body");
        }
        using var admin = fixture.Factory.Agent("willem-de-jong");
        (await admin.PostAsync("/api/knowledge/scan", null)).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.Agents.Where(a => a.Id == "codex").ExecuteUpdateAsync(setters => setters.SetProperty(a => a.MetadataJson,
            "{\"credential\":\"DO_NOT_RENDER\"}"));
    }

    private static string ToolManifest(string permission) =>
        $"id: production\nname: Production tool\ndescription: DO_NOT_RENDER\nprovider: mcp\ntags: []\npermissions: [{permission}]\ncapabilities: [deploy]";
    private static async Task<GraphResult> Get(HttpClient client, string path) => (await client.GetFromJsonAsync<GraphResult>(path))!;
    private static void AssertEndpoints(GraphResult graph, GraphRelationship edge)
    {
        Assert.Contains(graph.Entities, node => node.Id == edge.Source);
        Assert.Contains(graph.Entities, node => node.Id == edge.Target);
    }
}
