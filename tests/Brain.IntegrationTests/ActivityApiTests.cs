extern alias worker;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using Npgsql;
using ActivityEvent = Brain.Application.ActivityEvent;
using ActivityRetentionCleanup = worker::ActivityRetentionCleanup;

namespace Brain.IntegrationTests;

public sealed class ActivityApiTests(BrainFixture fixture) : IClassFixture<BrainFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false) } };
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    public async Task Incoming_w3c_context_is_retained_and_completed_requests_are_queryable(string flags)
    {
        var trace = NewTrace();
        using var client = fixture.Factory.Agent("codex");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agents/me");
        request.Headers.Add("traceparent", $"00-{trace}-2222222222222222-{flags}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal(trace, response.Headers.GetValues("X-Trace-Id").Single());
        var summary = await Completed(client, trace);
        Assert.Equal("codex", summary.AgentId);
        Assert.Equal(ActivityStatus.Succeeded, summary.Status);
        Assert.True(summary.DurationMs >= 0);
        var events = await Events(client, trace);
        Assert.Equal(["agent.request.started", "agent.request.completed"], events.Select(e => e.EventType));
        Assert.All(events, e => Assert.NotNull(e.ParentSpanId));
        Assert.All(events, e => Assert.Equal("agent:codex", e.SourceEntityId));
    }

    [Fact]
    public async Task Requests_without_context_create_distinct_concurrent_traces()
    {
        using var client = fixture.Factory.Agent("codex");
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.GetAsync("/api/agents/me")));
        var traces = responses.Select(r => r.Headers.GetValues("X-Trace-Id").Single()).ToArray();
        Assert.Equal(12, traces.Distinct().Count());
        foreach (var trace in traces)
        {
            Assert.Equal(32, trace.Length);
            Assert.Equal(ActivityStatus.Succeeded, (await Completed(client, trace)).Status);
            Assert.All(await Events(client, trace), e => Assert.Equal(trace, e.TraceId));
        }
    }

    [Fact]
    public async Task Failure_records_child_and_root_failure_without_request_or_exception_payloads()
    {
        using var client = fixture.Factory.Agent("codex");
        var response = await client.GetAsync("/api/skills/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var trace = response.Headers.GetValues("X-Trace-Id").Single();
        Assert.Equal(ActivityStatus.Failed, (await Completed(client, trace)).Status);
        var events = await Events(client, trace);
        var child = Assert.Single(events, e => e.EventType == "brain.skill.read.failed");
        Assert.Equal("BrainException", child.ErrorType);
        Assert.Equal("Operation failed.", child.ErrorMessage);
        Assert.Equal(ActivityStatus.Failed, events.Single(e => e.EventType == "agent.request.failed").Status);
    }

    [Fact]
    public async Task Skill_selection_maps_agent_skill_and_required_tool_without_executing_it()
    {
        SeedRegistry();
        using var client = fixture.Factory.Agent("codex");
        var response = await client.GetAsync("/api/skills/deploy-service");
        response.EnsureSuccessStatusCode();
        var trace = response.Headers.GetValues("X-Trace-Id").Single();
        await Completed(client, trace);
        var events = await Events(client, trace);
        Assert.Contains(events, e => e.EventType == "skill.selected" && e.SourceEntityId == "agent:codex" && e.TargetEntityId == "skill:deploy-service");
        Assert.Contains(events, e => e.SourceEntityId == "skill:deploy-service" && e.TargetEntityId == "tool:coolify");
        Assert.DoesNotContain(events, e => e.EventType == "tool.invoked");
        var root = events.Single(e => e.EventType == "agent.request.started");
        Assert.Equal(root.SpanId, events.Single(e => e.EventType == "brain.skill.read.started").ParentSpanId);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BrainDbContext>().RegistryEntries.CountAsync());
        Assert.True(await scope.ServiceProvider.GetRequiredService<BrainDbContext>().AuditEvents.AnyAsync());
    }

    [Fact]
    public async Task Trace_id_reuse_cannot_reveal_another_agents_events()
    {
        var trace = NewTrace();
        using var agent = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        foreach (var client in new[] { agent, admin })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agents/me");
            request.Headers.Add("traceparent", $"00-{trace}-2222222222222222-01");
            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        }
        await Completed(agent, trace);
        await WaitFor(async () => (await Events(admin, trace)).Count(e => e.IsRoot && e.Status == ActivityStatus.Succeeded) == 2);
        Assert.All(await Events(agent, trace), e => Assert.Equal("codex", e.AgentId));
        Assert.Empty((await agent.GetFromJsonAsync<TraceView[]>($"/api/traces?trace={trace}&agent=willem-de-jong", Json))!);
        var adminTraces = (await admin.GetFromJsonAsync<TraceView[]>($"/api/traces?trace={trace}", Json))!;
        Assert.Equal(2, adminTraces.Length);
    }

    [Fact]
    public async Task Sanitization_preserves_authenticated_ownership_even_when_an_id_matches_a_sensitive_pattern()
    {
        var trace = NewTrace();
        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IActivityStore>();
        await store.AppendAsync(Event(trace, "secret-agent", "agent.request.started", ActivityStatus.Started), default);
        await store.AppendAsync(Event(trace, "redacted", "agent.request.started", ActivityStatus.Started), default);
        Assert.Equal("secret-agent", Assert.Single(await store.EventsAsync(new(Trace: trace), "secret-agent", default)).AgentId);
        Assert.Equal("redacted", Assert.Single(await store.EventsAsync(new(Trace: trace), "redacted", default)).AgentId);
    }

    [Fact]
    public async Task Trace_and_live_endpoints_require_explicit_grants()
    {
        using var admin = fixture.Factory.Agent("willem-de-jong");
        (await admin.PostAsJsonAsync("/api/agents", new CreateAgentRequest("other", "Other", Roles: [], Permissions: []))).EnsureSuccessStatusCode();
        using var limited = fixture.Factory.Agent("other");
        using var anonymous = fixture.Factory.CreateClient();
        foreach (var path in new[] { "/api/traces", $"/api/traces/{NewTrace()}", $"/api/traces/{NewTrace()}/events", "/api/activity/stream" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/traces/{NewTrace()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/traces?status=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/traces?trace=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/traces?limit=1001")).StatusCode);
    }

    [Fact]
    public async Task Live_sse_filters_notifications_from_an_independent_worker_connection_even_without_retention()
    {
        var trace = NewTrace();
        using var client = fixture.Factory.Agent("codex");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await client.GetAsync($"/api/activity/stream?trace={trace}&tool=coolify&status=started&eventType=tool.invoked",
            HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        Assert.Equal("retry: 3000", await reader.ReadLineAsync(timeout.Token));
        Assert.Equal(": connected", await reader.ReadLineAsync(timeout.Token));
        Assert.Equal("", await reader.ReadLineAsync(timeout.Token));
        using var scope = fixture.Factory.Services.CreateScope();
        var connection = fixture.ConnectionString;
        await using var workerDataSource = NpgsqlDataSource.Create(connection);
        var workerBus = new ActivityLiveBus(workerDataSource, new() { RetentionDays = 0 },
            NullLogger<ActivityLiveBus>.Instance, new ActivitySanitizer(new ConfigurationBuilder().Build()));
        var expected = Event(trace, "codex", "tool.invoked", ActivityStatus.Started) with { TargetEntityId = "tool:coolify" };
        await new ActivityStore(scope.ServiceProvider.GetRequiredService<BrainDbContext>(), new() { RetentionDays = 0 },
            scope.ServiceProvider.GetRequiredService<ActivitySanitizer>()).AppendAsync(expected, timeout.Token);
        await workerBus.PublishAsync(expected with { Id = Guid.NewGuid(), AgentId = "willem-de-jong" }, timeout.Token);
        await workerBus.PublishAsync(expected with { Id = Guid.NewGuid(), TraceId = NewTrace() }, timeout.Token);
        await workerBus.PublishAsync(expected with { Id = Guid.NewGuid(), Status = ActivityStatus.Failed }, timeout.Token);
        await workerBus.PublishAsync(expected, timeout.Token);
        Assert.StartsWith("id: ", await reader.ReadLineAsync(timeout.Token));
        Assert.Equal("event: activity", await reader.ReadLineAsync(timeout.Token));
        var data = await reader.ReadLineAsync(timeout.Token);
        var delivered = JsonSerializer.Deserialize<ActivityEvent>(data!["data: ".Length..], Json)!;
        Assert.Equal(expected.Id, delivered.Id);
        Assert.Equal(expected.TraceId, delivered.TraceId);
        Assert.Equal("codex", delivered.AgentId);
        Assert.Equal("tool:coolify", delivered.TargetEntityId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IActivityStore>().ListAsync(new(Trace: trace), null, default));
        timeout.Cancel();
    }

    [Fact]
    public async Task Live_stream_rechecks_grants_and_closes_after_permission_revocation()
    {
        using var client = fixture.Factory.Agent("codex");
        using var admin = fixture.Factory.Agent("willem-de-jong");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var response = await client.GetAsync("/api/activity/stream", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        Assert.Equal("retry: 3000", await reader.ReadLineAsync(timeout.Token));
        Assert.Equal(": connected", await reader.ReadLineAsync(timeout.Token));
        Assert.Equal("", await reader.ReadLineAsync(timeout.Token));
        (await admin.PutAsJsonAsync("/api/agents/codex/access",
            new UpdateAccessRequest([], [Permissions.ActivityRead, Permissions.TracesRead]), timeout.Token)).EnsureSuccessStatusCode();
        Assert.Equal("event: stream.error", await reader.ReadLineAsync(timeout.Token));
        Assert.Contains("Access changed", await reader.ReadLineAsync(timeout.Token));
        timeout.Cancel();
    }

    [Fact]
    public async Task Multiple_root_operations_in_one_trace_stay_active_until_all_roots_complete_and_child_failure_wins()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IActivityStore>();
        var trace = NewTrace();
        var start = Event(trace, "codex", "agent.request.started", ActivityStatus.Started) with { IsRoot = true };
        var second = start with { Id = Guid.NewGuid(), SpanId = "cccccccccccccccc", Timestamp = start.Timestamp.AddMilliseconds(1) };
        await store.AppendAsync(start, default);
        await store.AppendAsync(second, default);
        await store.AppendAsync(start with { Id = Guid.NewGuid(), EventType = "agent.request.completed", Status = ActivityStatus.Succeeded }, default);
        Assert.Equal(ActivityStatus.Running, Assert.Single(await store.ListAsync(new(Trace: trace), "codex", default)).Status);
        await store.AppendAsync(Event(trace, "codex", "tool.failed", ActivityStatus.Failed), default);
        await store.AppendAsync(second with { Id = Guid.NewGuid(), EventType = "agent.request.completed", Status = ActivityStatus.Succeeded }, default);
        Assert.Equal(ActivityStatus.Failed, Assert.Single(await store.ListAsync(new(Trace: trace), "codex", default)).Status);
        // Appending the same event twice is idempotent.
        await store.AppendAsync(second, default);
        Assert.Equal(5, (await store.EventsAsync(new(Trace: trace), "codex", default)).Count);
    }

    [Fact]
    public async Task Retention_cascades_events_but_preserves_recent_activity_and_does_not_touch_audits()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IActivityStore>();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        var auditCount = await db.AuditEvents.CountAsync();
        var old = Event(NewTrace(), "codex", "agent.request.started", ActivityStatus.Started) with
        { IsRoot = true, Timestamp = DateTimeOffset.UtcNow.AddDays(-10) };
        await store.AppendAsync(old, default);
        await store.AppendAsync(old with { Id = Guid.NewGuid(), EventType = "agent.request.completed", Status = ActivityStatus.Succeeded }, default);
        var recent = old with { Id = Guid.NewGuid(), TraceId = NewTrace() };
        await store.AppendAsync(recent, default);
        await store.AppendAsync(recent with { Id = Guid.NewGuid(), IsRoot = false, Timestamp = DateTimeOffset.UtcNow }, default);
        var abandoned = old with { Id = Guid.NewGuid(), TraceId = NewTrace() };
        await store.AppendAsync(abandoned, default);
        Assert.Equal(2, await store.DeleteBeforeAsync(DateTimeOffset.UtcNow.AddDays(-7), default));
        Assert.Empty(await store.EventsAsync(new(Trace: old.TraceId), "codex", default));
        Assert.Empty(await store.EventsAsync(new(Trace: abandoned.TraceId), "codex", default));
        Assert.Equal(2, (await store.EventsAsync(new(Trace: recent.TraceId), "codex", default)).Count);
        Assert.Equal(auditCount, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Real_retention_worker_publishes_job_events_and_removes_expired_history()
    {
        var trace = NewTrace();
        using (var scope = fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IActivityStore>().AppendAsync(
                Event(trace, "codex", "agent.request.started", ActivityStatus.Started) with
                { IsRoot = true, Timestamp = DateTimeOffset.UtcNow.AddDays(-10) }, default);
        using var worker = new ActivityRetentionCleanup(
            fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(), new(),
            TimeProvider.System, NullLogger<ActivityRetentionCleanup>.Instance);
        await worker.StartAsync(default);
        try
        {
            using var admin = fixture.Factory.Agent("willem-de-jong");
            await WaitFor(async () =>
            {
                var traces = (await admin.GetFromJsonAsync<TraceView[]>("/api/traces?agent=brain-worker&status=succeeded", Json))!;
                return traces.Any(t => t.RootAction == "activity-retention");
            });
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/traces/{trace}")).StatusCode);
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task Worker_exceptions_emit_failed_job_traces_and_do_not_expose_diagnostics()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => worker::WorkerActivity.TraceJobAsync<int>(fixture.Factory.Services, "test-failure",
            () => throw new HttpRequestException("Password=sensitive-worker-value"), default));
        using var admin = fixture.Factory.Agent("willem-de-jong");
        TraceView? trace = null;
        await WaitFor(async () =>
        {
            trace = (await admin.GetFromJsonAsync<TraceView[]>("/api/traces?agent=brain-worker&status=failed", Json))!
                .SingleOrDefault(t => t.RootAction == "test-failure");
            return trace is not null;
        });
        var events = await Events(admin, trace!.TraceId);
        Assert.Equal(["worker.job.started", "worker.job.failed"], events.Select(e => e.EventType));
        Assert.DoesNotContain("sensitive-worker-value", JsonSerializer.Serialize(events, Json));
    }

    [Fact]
    public async Task Slow_live_subscriber_is_disconnected_explicitly_instead_of_growing_unbounded()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var connection = fixture.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(connection);
        var bus = new ActivityLiveBus(dataSource, new() { SubscriberCapacity = 1 }, NullLogger<ActivityLiveBus>.Instance,
            scope.ServiceProvider.GetRequiredService<ActivitySanitizer>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var trace = NewTrace();
        await using var subscription = await bus.SubscribeAsync(new(Trace: trace), "codex", timeout.Token);
        for (var i = 0; i < 8; i++)
            await bus.PublishAsync(Event(trace, "codex", "worker.job.started", ActivityStatus.Started), timeout.Token);
        await Task.Delay(100, timeout.Token);
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await foreach (var _ in subscription.ReadAllAsync(timeout.Token)) { }
        });
    }

    [Fact]
    public async Task Trace_summary_and_event_filters_match_entity_type_status_and_time()
    {
        var trace = NewTrace();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IActivityStore>();
            var start = Event(trace, "codex", "tool.invoked", ActivityStatus.Started) with
            { IsRoot = true, SourceEntityId = "skill:deploy-service", TargetEntityId = "tool:coolify" };
            await store.AppendAsync(start, default);
            await store.AppendAsync(start with { Id = Guid.NewGuid(), EventType = "tool.completed", Status = ActivityStatus.Succeeded }, default);
            await store.AppendAsync(Event(trace, "codex", "external.request.completed", ActivityStatus.Succeeded) with
            { SourceEntityId = "tool:coolify", TargetEntityId = "service:coolify" }, default);
        }
        using var client = fixture.Factory.Agent("codex");
        Assert.Single((await client.GetFromJsonAsync<TraceView[]>($"/api/traces?trace={trace}&tool=coolify&skill=deploy-service&status=succeeded", Json))!);
        Assert.Single((await client.GetFromJsonAsync<ActivityEvent[]>($"/api/traces/{trace}/events?service=coolify&status=succeeded", Json))!);
        Assert.Empty((await client.GetFromJsonAsync<ActivityEvent[]>($"/api/traces/{trace}/events?entity=skill:missing", Json))!);
        Assert.Empty((await client.GetFromJsonAsync<TraceView[]>($"/api/traces?trace={trace}&from=2100-01-01T00:00:00Z", Json))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/traces?from=2030-01-01T00:00:00Z&to=2020-01-01T00:00:00Z")).StatusCode);
    }

    [Fact]
    public async Task Mcp_tool_and_skill_events_share_http_context_and_parent_spans()
    {
        SeedRegistry();
        using var http = fixture.Factory.Agent("codex");
        var trace = NewTrace();
        http.DefaultRequestHeaders.Add("traceparent", $"00-{trace}-2222222222222222-01");
        await using var transport = new HttpClientTransport(new()
        { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp, EnableStandaloneGetStream = false }, http,
            ownsHttpClient: false);
        await using var mcp = await McpClient.CreateAsync(transport);
        var result = await mcp.CallToolAsync("brain_get_skill", new Dictionary<string, object?> { ["skillId"] = "deploy-service" });
        Assert.False(result.IsError is true);
        await WaitFor(async () => (await Events(http, trace)).Any(e => e.EventType == "tool.completed"));
        var events = await Events(http, trace);
        var request = Assert.Single(events, e => e.EventType == "mcp.request.started");
        var tool = Assert.Single(events, e => e.EventType == "tool.invoked");
        Assert.Equal(request.SpanId, tool.ParentSpanId);
        Assert.Contains(events, e => e.EventType == "skill.selected" && e.ParentSpanId == tool.SpanId);
    }

    [Fact]
    public async Task Native_http_propagates_context_and_records_external_failure_without_url_or_header_secrets()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var server = builder.Build();
        string? propagated = null;
        server.MapGet("/", (Microsoft.AspNetCore.Http.HttpContext context) =>
        {
            propagated = context.Request.Headers["traceparent"];
            return Microsoft.AspNetCore.Http.Results.StatusCode(503);
        });
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var publisher = fixture.Factory.Services.GetRequiredService<IActivityPublisher>();
        string trace;
        using (publisher.Start(new("agent.test", "agent.request.started", "agent.request.completed", "agent.request.failed",
            "agent:codex", "brain", "codex")))
        {
            trace = Activity.Current!.TraceId.ToHexString();
            using (publisher.Start(new("tool.test", "tool.invoked", "tool.completed", "tool.failed", "skill:deploy-service", "tool:coolify")))
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = new("Bearer", "outbound-sensitive-value");
                Assert.Equal(HttpStatusCode.ServiceUnavailable, (await http.GetAsync(address + "/?apiKey=secret-query-value")).StatusCode);
            }
        }
        Assert.Contains(trace, propagated);
        using var client = fixture.Factory.Agent("codex");
        Assert.Equal(ActivityStatus.Failed, (await Completed(client, trace)).Status);
        var events = await Events(client, trace);
        var external = Assert.Single(events, e => e.EventType == "external.request.failed");
        Assert.Equal(events.Single(e => e.EventType == "tool.invoked").SpanId, external.ParentSpanId);
        Assert.Equal("tool:coolify", external.SourceEntityId);
        var json = JsonSerializer.Serialize(events, Json);
        Assert.DoesNotContain("secret-query-value", json);
        Assert.DoesNotContain("outbound-sensitive-value", json);
        Assert.Contains("503", json);
    }

    private static string NewTrace() => ActivityTraceId.CreateRandom().ToHexString();
    private static ActivityEvent Event(string trace, string agent, string eventType, ActivityStatus status) =>
        new(Guid.NewGuid(), trace, "bbbbbbbbbbbbbbbb", null, agent, eventType, "test.operation", status, DateTimeOffset.UtcNow);
    private static async Task<ActivityEvent[]> Events(HttpClient client, string trace) =>
        (await client.GetFromJsonAsync<ActivityEvent[]>($"/api/traces/{trace}/events?limit=1000", Json))!;
    private static async Task<TraceView> Completed(HttpClient client, string trace)
    {
        TraceView? result = null;
        await WaitFor(async () =>
        {
            using var response = await client.GetAsync($"/api/traces/{trace}");
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadFromJsonAsync<TraceView>(Json);
            return result!.CompletedAt is not null;
        });
        return result!;
    }
    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!await condition())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(15), "Activity dispatch did not reach the expected state.");
            await Task.Delay(25);
        }
    }
    private void SeedRegistry()
    {
        var skill = Path.Combine(fixture.RegistryPath, "skills", "deploy-service");
        var tool = Path.Combine(fixture.RegistryPath, "tools", "coolify");
        Directory.CreateDirectory(skill); Directory.CreateDirectory(tool);
        File.WriteAllText(Path.Combine(skill, "manifest.yaml"), """
            id: deploy-service
            name: Deploy Service
            description: Deploy a service.
            tags: []
            required_tools: [coolify]
            required_permissions: []
            """);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), "# Deploy\nInstructions only.");
        File.WriteAllText(Path.Combine(tool, "manifest.yaml"), """
            id: coolify
            name: Coolify
            description: Manage deployments.
            provider: http
            permissions: []
            capabilities: [deploy_service]
            """);
    }
}
