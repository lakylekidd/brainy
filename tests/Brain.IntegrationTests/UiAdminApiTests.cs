using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.IntegrationTests;

public sealed class UiAdminApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>
{
    [Fact]
    public async Task Token_only_administrator_uses_current_database_permissions_and_disable_state()
    {
        await fixture.ResetAsync();
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BrainFactory.UiAdminToken);
        var identity = await client.GetFromJsonAsync<JsonElement>("/api/agents/me");
        Assert.Equal("willem-de-jong", identity.GetProperty("id").GetString());
        Assert.Contains(Permissions.AgentsRead,
            identity.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/agents")).StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
            var admin = await db.Agents.Include(agent => agent.Roles).Include(agent => agent.Permissions)
                .SingleAsync(agent => agent.Id == "willem-de-jong");
            admin.Roles.Clear();
            admin.Permissions.Clear();
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/agents")).StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
            var admin = await db.Agents.SingleAsync(agent => agent.Id == "willem-de-jong");
            admin.Enabled = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents/me")).StatusCode);
    }
}
