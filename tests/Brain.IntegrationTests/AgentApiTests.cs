using System.Net;
using System.Net.Http.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class AgentApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Details_distinguish_direct_inherited_and_effective_grants_and_catalogs_include_unassigned_roles()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
            db.Roles.Add(new Role { Id = "unassigned", Description = "No members" });
            await db.SaveChangesAsync();
        }
        var created = await admin.PostAsJsonAsync("/api/agents",
            new CreateAgentRequest("other", "Other", Roles: ["agent"], Permissions: [Permissions.MemoryRead, Permissions.AgentsRead]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = (await admin.GetFromJsonAsync<AgentView>(created.Headers.Location))!;
        Assert.Equal(["agent"], detail.Roles);
        Assert.Equal(new[] { Permissions.AgentsRead, Permissions.MemoryRead }, detail.DirectPermissions);
        Assert.Contains(Permissions.MemoryWrite, detail.Permissions);
        Assert.Equal(detail.Permissions.Length, detail.Permissions.Distinct().Count());
        var listed = (await admin.GetFromJsonAsync<AgentView[]>("/api/agents"))!.Single(a => a.Id == detail.Id);
        Assert.Equal(detail.DirectPermissions, listed.DirectPermissions);
        var roles = (await admin.GetFromJsonAsync<RoleView[]>("/api/roles"))!;
        Assert.Contains(roles, r => r.Id == "unassigned" && r.Description == "No members" && r.Permissions.Length == 0);
        Assert.Contains(Permissions.MemoryRead, roles.Single(r => r.Id == "agent").Permissions);
        var permissions = (await admin.GetFromJsonAsync<PermissionView[]>("/api/permissions"))!;
        Assert.Equal(Permissions.All.Order(), permissions.Select(p => p.Id));
        Assert.All(permissions, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/agents/missing/details")).StatusCode);
    }

    [Theory]
    [InlineData("/api/agents")]
    [InlineData("/api/agents/willem-de-jong/details")]
    [InlineData("/api/agents/missing/details")]
    [InlineData("/api/roles")]
    [InlineData("/api/permissions")]
    public async Task Inspection_requires_current_read_grant_before_disclosing_existence(string path)
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var agent = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync(path)).StatusCode);
        using var admin = fixture.Factory.Agent("willem-de-jong");
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], [Permissions.AgentsRead])))
            .EnsureSuccessStatusCode();
        Assert.Equal(path.Contains("/missing/", StringComparison.Ordinal) ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            (await agent.GetAsync(path)).StatusCode);
        (await admin.PutAsJsonAsync("/api/agents/codex/access", new UpdateAccessRequest([], []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Read_only_inspection_does_not_grant_management_and_failed_mutations_preserve_access()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Reader",
            Permissions: [Permissions.AgentsRead]))).EnsureSuccessStatusCode();
        using var reader = fixture.Factory.Agent("other");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/agents", new CreateAgentRequest("new", "New"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync("/api/agents/codex/access",
            new UpdateAccessRequest(["administrator"], []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/agents/willem-de-jong/access",
            new UpdateAccessRequest([], [], false))).StatusCode);
        foreach (var request in new[]
        {
            new UpdateAccessRequest(["missing"], []),
            new UpdateAccessRequest([], ["unknown.permission"]),
            new UpdateAccessRequest(null!, []),
            new UpdateAccessRequest([], Enumerable.Repeat(Permissions.MemoryRead, 101).ToArray())
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/agents/other/access", request)).StatusCode);
        var other = (await admin.GetFromJsonAsync<AgentView>("/api/agents/other/details"))!;
        Assert.Equal(new[] { Permissions.AgentsRead }, other.DirectPermissions);
        Assert.True(other.Enabled);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Duplicate"))).StatusCode);
    }

    [Fact]
    public async Task Role_creation_requires_admin_validates_known_grants_and_audits_atomically()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var reader = fixture.Factory.Agent("codex");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("reviewer", "Review knowledge", [Permissions.KnowledgeRead]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("invalid role", "Invalid", []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("reviewer", "Invalid grant", ["unknown.permission"]))).StatusCode);

        var response = await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("reviewer", "Review knowledge", [Permissions.KnowledgeRead]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RoleView>())!;
        Assert.Equal("reviewer", created.Id);
        Assert.Equal([Permissions.KnowledgeRead], created.Permissions);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("reviewer", "Duplicate", []))).StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Resource == "reviewer" && a.Action == "brain.role.created"));
    }

    [Fact]
    public async Task Supported_management_replaces_grants_reports_disabled_details_and_audits_atomically()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Other", Roles: ["agent"]))).EnsureSuccessStatusCode();
        var response = await admin.PutAsJsonAsync("/api/agents/other/access",
            new UpdateAccessRequest([], [Permissions.AgentsRead], false));
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<AgentView>())!;
        Assert.False(updated.Enabled);
        Assert.Empty(updated.Roles);
        Assert.Equal(new[] { Permissions.AgentsRead }, updated.Permissions);
        Assert.Equal(updated.DirectPermissions, updated.Permissions);
        Assert.False((await admin.GetFromJsonAsync<AgentView>("/api/agents/other/details"))!.Enabled);
        using var other = fixture.Factory.Agent("other");
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/agents/me")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.Resource == "other" &&
            (a.Action == "brain.agent.created" || a.Action == "brain.agent.access_updated")));
    }

    [Fact]
    public async Task Identity_named_me_has_independent_details_without_shadowing_current_identity()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        var created = await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("me", "An identity named me"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("me", (await admin.GetFromJsonAsync<AgentView>(created.Headers.Location))!.Id);
        var current = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/agents/me");
        Assert.Equal("willem-de-jong", current.GetProperty("id").GetString());
    }
}
