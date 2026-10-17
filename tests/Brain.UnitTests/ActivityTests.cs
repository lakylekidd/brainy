using System.Diagnostics;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using ActivityEvent = Brain.Application.ActivityEvent;

namespace Brain.UnitTests;

public sealed class ActivityTests
{
    [Fact]
    public void Creates_w3c_trace_and_preserves_nested_agent_skill_tool_service_path()
    {
        var harness = new ActivityHarness();
        using (harness.Publisher.Start(Operation("agent", "agent:codex", "brain", "codex")))
        using (harness.Publisher.Start(Operation("skill", "agent:codex", "skill:deploy-service")))
        using (harness.Publisher.Start(Operation("tool", "skill:deploy-service", "tool:coolify")))
        using (harness.Publisher.Start(Operation("external", "tool:coolify", "service:coolify"))) { }
        var events = harness.Drain();
        Assert.Equal(8, events.Length);
        Assert.Single(events.Select(e => e.TraceId).Distinct());
        var started = events.Where(e => e.Status == ActivityStatus.Started).ToArray();
        Assert.All(started, e => { Assert.Equal(32, e.TraceId.Length); Assert.Equal(16, e.SpanId.Length); Assert.Equal("codex", e.AgentId); });
        Assert.Equal(started[0].SpanId, started[1].ParentSpanId);
        Assert.Equal(started[1].SpanId, started[2].ParentSpanId);
        Assert.Equal(started[2].SpanId, started[3].ParentSpanId);
        Assert.Equal("skill:deploy-service", started[2].SourceEntityId);
        Assert.Equal("tool:coolify", started[3].SourceEntityId);
        Assert.All(events.Where(e => e.Status != ActivityStatus.Started), e => Assert.Equal(ActivityStatus.Succeeded, e.Status));
        Assert.True(events[^1].IsRoot);
        Assert.NotNull(events[^1].DurationMs);
    }

    [Fact]
    public void Incoming_traceparent_is_preserved_but_baggage_cannot_select_owner()
    {
        using var incoming = new Activity("server").SetIdFormat(ActivityIdFormat.W3C)
            .SetParentId("00-11111111111111111111111111111111-2222222222222222-01").Start();
        incoming.SetBaggage("agent", "administrator");
        var harness = new ActivityHarness();
        using (harness.Publisher.Start(Operation("brain", "agent:codex", "brain", "codex"))) { }
        var events = harness.Drain();
        Assert.All(events, e => Assert.Equal("11111111111111111111111111111111", e.TraceId));
        Assert.All(events, e => Assert.Equal(incoming.SpanId.ToHexString(), e.ParentSpanId));
        Assert.All(events, e => Assert.Equal("codex", e.AgentId));
    }

    [Fact]
    public async Task Child_failure_marks_root_failed_without_exposing_exception_text()
    {
        var harness = new ActivityHarness();
        using (harness.Publisher.Start(Operation("agent", "agent:codex", "brain", "codex")))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => harness.Publisher.RunAsync<int>(
                Operation("tool", "skill:deploy", "tool:coolify"), () => throw new HttpRequestException("Bearer confidential-value")));
        }
        var events = harness.Drain();
        Assert.Equal(ActivityStatus.Failed, events[^1].Status);
        Assert.Equal("HttpRequestException", events.Single(e => e.EventType == "tool.failed").ErrorType);
        Assert.DoesNotContain("confidential-value", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task Cancellation_is_not_reported_as_success_or_failure()
    {
        var harness = new ActivityHarness();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Publisher.RunAsync<int>(
            Operation("agent", "agent:codex", "brain", "codex"), () => throw new OperationCanceledException(stop.Token), stop.Token));
        Assert.Equal(ActivityStatus.Cancelled, harness.Drain()[^1].Status);
    }

    [Fact]
    public async Task Concurrent_operations_keep_separate_context_and_restore_the_parent()
    {
        var harness = new ActivityHarness();
        var previous = Activity.Current;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
        {
            using (harness.Publisher.Start(Operation("agent", $"agent:a{i}", "brain", $"a{i}")))
            {
                await Task.Yield();
                using (harness.Publisher.Start(Operation("tool", "skill:deploy", "tool:coolify"))) await Task.Delay(5);
            }
        })));
        var events = harness.Drain();
        Assert.Equal(16, events.Select(e => e.TraceId).Distinct().Count());
        Assert.All(events.GroupBy(e => e.TraceId), group => Assert.Single(group.Select(e => e.AgentId).Distinct()));
        Assert.Same(previous, Activity.Current);
    }

    [Fact]
    public void Central_redaction_drops_bodies_headers_arbitrary_metadata_and_configured_secret_values()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Metadata:Semantic:ApiKey"] = "configured-sensitive-value", ["ConnectionStrings:Brain"] = "sensitive-db-connection" }).Build();
        var sanitizer = new ActivitySanitizer(configuration);
        var result = sanitizer.Sanitize(new(Guid.NewGuid(), new string('a', 32), new string('b', 16), null, "codex",
            "tool.failed", "call", ActivityStatus.Failed, DateTimeOffset.UtcNow,
            "tool:configured-sensitive-value", ErrorType: "TimeoutException",
            ErrorMessage: "Password=password; Authorization: Bearer value",
            Metadata: new Dictionary<string, string>
            {
                ["body"] = "secret-body", ["Authorization"] = "Bearer header", ["apiKey"] = "my-key",
                ["password"] = "password", ["Infisical"] = "secret", ["connectionString"] = "sensitive-db-connection",
                ["project"] = "configured-sensitive-value", ["service"] = "coolify", ["http.method"] = "GET",
                ["http.status_code"] = "503", ["tool"] = "Bearer header"
            }));
        var json = JsonSerializer.Serialize(result);
        foreach (var secret in new[] { "configured-sensitive-value", "sensitive-db-connection", "Bearer", "secret-body", "my-key", "password" })
            Assert.DoesNotContain(secret, json);
        Assert.Equal("coolify", result.Metadata!["service"]);
        Assert.Equal("GET", result.Metadata["http.method"]);
        Assert.Equal("503", result.Metadata["http.status_code"]);
        Assert.Equal("Operation failed.", result.ErrorMessage);
        Assert.Equal("redacted", result.SourceEntityId);
    }

    [Fact]
    public void Export_processor_removes_urls_queries_headers_baggage_and_status_descriptions()
    {
        using var activity = new Activity("http.request").Start();
        activity.SetTag("url.full", "https://user:pass@host/?apiKey=private");
        activity.SetTag("http.request.header.authorization", "Bearer private");
        activity.SetTag("brain.password", "opaque-private-value");
        activity.SetTag("http.request.method", "POST");
        activity.SetTag("http.response.status_code", 503);
        activity.SetBaggage("private", "private-value");
        activity.SetStatus(ActivityStatusCode.Error, "sensitive failure");
        activity.AddEvent(new System.Diagnostics.ActivityEvent("exception",
            tags: new ActivityTagsCollection { ["exception.message"] = "private-exception" }));
        new SanitizingActivityProcessor(new ActivitySanitizer(new ConfigurationBuilder().Build()),
            NullLogger<SanitizingActivityProcessor>.Instance).OnEnd(activity);
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("http.request.header.authorization"));
        Assert.Null(activity.GetTagItem("brain.password"));
        Assert.Equal("POST", activity.GetTagItem("http.request.method"));
        Assert.Equal("Operation failed.", activity.StatusDescription);
        Assert.Empty(activity.Baggage);
        Assert.False(activity.Recorded);
    }

    [Fact]
    public void Unsafe_immutable_event_payloads_are_not_sent_to_any_exporter()
    {
        using var exporter = new RecordingExporter();
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource("brain-redaction-test")
            .AddProcessor(new SanitizingActivityProcessor(new(new ConfigurationBuilder().Build()),
                NullLogger<SanitizingActivityProcessor>.Instance))
            .AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
        using var source = new ActivitySource("brain-redaction-test");
        using (source.StartActivity("safe.operation")) { }
        using (var activity = source.StartActivity("unsafe.operation"))
            activity!.AddEvent(new System.Diagnostics.ActivityEvent("exception",
                tags: new ActivityTagsCollection { ["exception.message"] = "private-value" }));
        Assert.Equal("safe.operation", Assert.Single(exporter.Spans).OperationName);
    }

    [Fact]
    public void Subscriber_filters_are_conjunctive_and_never_expand_owner_scope()
    {
        var e = new ActivityEvent(Guid.NewGuid(), new string('a', 32), new string('b', 16), null, "codex",
            "tool.invoked", "deploy", ActivityStatus.Started, DateTimeOffset.UtcNow, "skill:deploy", "tool:coolify");
        Assert.True(ActivityFiltering.Matches(e, new(Agent: "codex", Trace: e.TraceId, Entity: "skill:deploy",
            EventType: "tool.invoked", Status: ActivityStatus.Started, Tool: "coolify", Skill: "deploy"), "codex"));
        Assert.False(ActivityFiltering.Matches(e, new(Agent: "codex"), "other"));
        Assert.False(ActivityFiltering.Matches(e, new(Status: ActivityStatus.Failed), null));
        Assert.False(ActivityFiltering.Matches(e, new(Service: "coolify"), null));
        Assert.False(ActivityFiltering.Matches(e, new(From: e.Timestamp.AddSeconds(1)), null));
        Assert.Throws<BrainException>(() => ActivityQueryService.Validate(new(Trace: "bad")));
        Assert.Throws<BrainException>(() => ActivityQueryService.Validate(new(Limit: 1001)));
        Assert.Throws<BrainException>(() => ActivityQueryService.Owner(new("codex", null, new HashSet<string>()), Permissions.TracesRead));
    }

    [Fact]
    public void Redaction_cannot_coalesce_distinct_authenticated_owners()
    {
        var harness = new ActivityHarness();
        using (harness.Publisher.Start(Operation("agent", "agent:secret-agent", "brain", "secret-agent"))) { }
        using (harness.Publisher.Start(Operation("agent", "agent:redacted", "brain", "redacted"))) { }
        var events = harness.Drain();
        Assert.Equal(2, events.Count(e => e.AgentId == "secret-agent"));
        Assert.Equal(2, events.Count(e => e.AgentId == "redacted"));
        Assert.All(events.Where(e => ActivityFiltering.Matches(e, new(), "redacted")), e => Assert.Equal("redacted", e.AgentId));
    }

    private static ActivityOperation Operation(string area, string source, string target, string? owner = null) =>
        new(area, $"{area}.started", $"{area}.completed", $"{area}.failed", source, target, owner);

    private sealed class RecordingExporter : BaseExporter<Activity>
    {
        public List<Activity> Spans { get; } = [];
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) Spans.Add(activity);
            return ExportResult.Success;
        }
    }
}

internal sealed class ActivityHarness
{
    public ActivityEventQueue Queue { get; } = new(new(), NullLogger<ActivityEventQueue>.Instance);
    public BrainActivityPublisher Publisher { get; }
    public ActivityHarness() => Publisher = new(Queue, new(new ConfigurationBuilder().Build()), TimeProvider.System);
    public ActivityEvent[] Drain()
    {
        var result = new List<ActivityEvent>();
        while (Queue.Reader.TryRead(out var e)) result.Add(e);
        return result.ToArray();
    }
}
