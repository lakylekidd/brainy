using Brain.Application;
using Brain.Domain;
using Microsoft.AspNetCore.Http.Features;

namespace Brain.Api;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapBrainDevices(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        var bindings = new Dictionary<string, string>(configuration.GetSection("Authentication:HumanAgentBindings")
            .Get<Dictionary<string, string>>() ?? [], StringComparer.Ordinal);
        app.MapPost("/api/devices/register", async (RegisterDeviceRequest request,
            HttpContext http, DeviceService devices, IAgentDirectory directory, CancellationToken ct) =>
        {
            var (subject, agent) = await HumanAsync(http, bindings, directory, ct);
            var result = await devices.RegisterAsync(subject, agent.Id, request, ct);
            return Results.Created($"/api/devices/{result.Device.Id}", result);
        }).RequireAuthorization(policy => policy.AddAuthenticationSchemes("HumanOidc").RequireAuthenticatedUser());

        app.MapGet("/api/devices", (RequestAgent agent, DeviceService devices, CancellationToken ct) =>
            devices.ListAsync(agent.Context, ct));
        app.MapGet("/api/devices/{id:guid}", (Guid id, RequestAgent agent, DeviceService devices, CancellationToken ct) =>
            devices.GetAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapPatch("/api/devices/{id:guid}", (Guid id, UpdateDeviceRequest request,
            RequestAgent agent, DeviceService devices, CancellationToken ct) =>
            devices.RenameAsync(agent.Context, agent.DeviceId, id, request, ct));
        app.MapPost("/api/devices/{id:guid}/heartbeat", (Guid id, DeviceHeartbeatRequest request,
            RequestAgent agent, DeviceService devices, CancellationToken ct) =>
            devices.HeartbeatAsync(agent.Context, agent.DeviceId, id, request, ct));
        app.MapPost("/api/devices/{id:guid}/rotate", (Guid id, RequestAgent agent,
            DeviceService devices, CancellationToken ct) =>
            devices.RotateAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapPost("/api/devices/{id:guid}/revoke", (Guid id, RequestAgent agent,
            DeviceService devices, CancellationToken ct) =>
            devices.RevokeAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapGet("/api/devices/{id:guid}/runtime", (Guid id, RequestAgent agent,
            RuntimeService runtime, CancellationToken ct) =>
            runtime.ListAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapPost("/api/devices/{id:guid}/runtime", (Guid id, RuntimeReportRequest request,
            RequestAgent agent, RuntimeService runtime, CancellationToken ct) =>
            runtime.ReportAsync(agent.DeviceId ?? Guid.Empty, id, request, ct));
        app.MapGet("/api/devices/{id:guid}/dependencies", (Guid id, RequestAgent agent,
            DependencyService dependencies, CancellationToken ct) =>
            dependencies.CheckAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapGet("/api/devices/{id:guid}/skills", (Guid id, RequestAgent agent,
            DeviceSkillService skills, CancellationToken ct) =>
            skills.ListAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapPost("/api/devices/{id:guid}/skills/sync", (Guid id, DeviceSkillSyncRequest request,
            RequestAgent agent, DeviceSkillService skills, CancellationToken ct) =>
            skills.ReportAsync(agent.Context, agent.DeviceId, id, request, ct));
        app.MapPost("/api/devices/{id:guid}/provisioning", async (Guid id, ProvisionRequest request,
            HttpContext http, IAgentDirectory directory, ProvisioningService provisioning, CancellationToken ct) =>
        {
            var (_, actor) = await HumanAsync(http, bindings, directory, ct);
            var plan = await provisioning.PlanAsync(actor, id, request, ct);
            return Results.Created($"/api/devices/{id}/provisioning/{plan.Id}", plan);
        }).RequireAuthorization(policy => policy.AddAuthenticationSchemes("HumanOidc").RequireAuthenticatedUser());
        app.MapPost("/api/devices/{id:guid}/provisioning/{planId:guid}/approve", async (Guid id, Guid planId,
            HttpContext http, IAgentDirectory directory, ProvisioningService provisioning, CancellationToken ct) =>
        {
            var (subject, actor) = await HumanAsync(http, bindings, directory, ct);
            return await provisioning.ApproveAsync(actor, subject, id, planId, ct);
        }).RequireAuthorization(policy => policy.AddAuthenticationSchemes("HumanOidc").RequireAuthenticatedUser());
        app.MapGet("/api/devices/{id:guid}/provisioning", (Guid id, RequestAgent agent,
            ProvisioningService provisioning, CancellationToken ct) =>
            provisioning.ListAsync(agent.Context, agent.DeviceId, id, ct));
        app.MapGet("/api/devices/{id:guid}/provisioning/queue", (Guid id, RequestAgent agent,
            ProvisioningService provisioning, CancellationToken ct) =>
            provisioning.QueueAsync(agent.DeviceId ?? Guid.Empty, id, ct));
        app.MapPost("/api/devices/{id:guid}/provisioning/{planId:guid}/start", (Guid id, Guid planId,
            RequestAgent agent, ProvisioningService provisioning, CancellationToken ct) =>
            provisioning.StartAsync(agent.DeviceId ?? Guid.Empty, id, planId, ct));
        app.MapPost("/api/devices/{id:guid}/provisioning/{planId:guid}/complete", (Guid id, Guid planId,
            ProvisionResultRequest request, RequestAgent agent, ProvisioningService provisioning, CancellationToken ct) =>
            provisioning.CompleteAsync(agent.DeviceId ?? Guid.Empty, id, planId, request, ct));
        app.MapGet("/api/client/skill-events", async (HttpContext http, RequestAgent agent,
            IActivityStream stream, CancellationToken ct) =>
        {
            agent.Context.Require(Permissions.SkillsSync);
            using var lease = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lease.CancelAfter(TimeSpan.FromMinutes(1));
            await using var subscription = await stream.SubscribeAsync(new ActivityFilter(EventType: "brain.registry.indexed"), null, ct);
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache, no-store";
            http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            await http.Response.WriteAsync("retry: 30000\n: connected\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
            try
            {
                await foreach (var _ in subscription.ReadAllAsync(lease.Token))
                {
                    await http.Response.WriteAsync("event: skill.changed\ndata: {}\n\n", lease.Token);
                    await http.Response.Body.FlushAsync(lease.Token);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && lease.IsCancellationRequested) { }
        });
        return app;
    }

    private static async Task<(string Subject, AgentContext Agent)> HumanAsync(HttpContext http,
        IReadOnlyDictionary<string, string> bindings, IAgentDirectory directory, CancellationToken ct)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        var scopes = (http.User.FindFirst("scope")?.Value ?? http.User.FindFirst("scp")?.Value ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (subject is null || !bindings.TryGetValue(subject, out var agentId) ||
            !scopes.Contains("brain.device", StringComparer.Ordinal) ||
            await directory.ResolveAsync(agentId, ct) is not { } agent)
            throw new BrainException(403, "Human identity is not bound to an enabled Brain agent.");
        return (subject, agent);
    }
}
