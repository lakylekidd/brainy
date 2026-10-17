using System.Diagnostics;
using System.Threading.Channels;
using Brain.Application;
using Brain.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ActivityEvent = Brain.Application.ActivityEvent;

namespace Brain.Infrastructure;

public sealed class ActivityEventQueue(ActivityTracingOptions options, ILogger<ActivityEventQueue> logger)
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    private readonly Channel<ActivityEvent> events = Channel.CreateBounded<ActivityEvent>(
        new BoundedChannelOptions(options.QueueCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public ChannelReader<ActivityEvent> Reader => events.Reader;
    public void Complete() => events.Writer.TryComplete();
    public void Write(ActivityEvent e)
    {
        if (!events.Writer.TryWrite(e))
            logger.LogError("Activity queue is full or closed; dropped event {EventId}. Trace history may be incomplete", e.Id);
    }
}

public sealed class BrainActivityPublisher(ActivityEventQueue queue, ActivitySanitizer sanitizer, TimeProvider time) : IActivityPublisher
{
    private readonly ActivityEventQueue queue = queue;
    private readonly ActivitySanitizer sanitizer = sanitizer;
    private readonly TimeProvider time = time;
    public const string SourceName = "HomelabBrain";
    public static readonly ActivitySource Source = new(SourceName);

    public IActivityScope Start(ActivityOperation operation)
    {
        var parent = Activity.Current;
        var inheritedOwner = Owner(parent);
        var owner = inheritedOwner ?? operation.AgentId ?? "brain-worker";
        var activity = Source.StartActivity(sanitizer.Identifier(operation.Action), ActivityKind.Internal)
            ?? new Activity(sanitizer.Identifier(operation.Action)).SetIdFormat(ActivityIdFormat.W3C).Start();
        var root = inheritedOwner is null;
        activity.SetTag("brain.agent_id", owner);
        activity.SetCustomProperty("brain.owner", owner);
        activity.SetTag("brain.instance_id", queue.InstanceId);
        activity.SetTag("brain.source_entity_id", operation.SourceEntityId is null ? null : sanitizer.Identifier(operation.SourceEntityId));
        activity.SetTag("brain.target_entity_id", operation.TargetEntityId is null ? null : sanitizer.Identifier(operation.TargetEntityId));
        if (root) activity.SetTag("brain.root", true);
        var scope = new Scope(this, activity, operation, owner, root);
        scope.Emit(operation.StartedEvent, ActivityStatus.Started);
        return scope;
    }

    public void Publish(string eventType, ActivityStatus status, string? sourceEntityId = null, string? targetEntityId = null)
    {
        if (Activity.Current is not { } activity || Owner(activity) is not { } owner)
            throw new InvalidOperationException("Publish activity inside an authenticated operation scope.");
        if (status == ActivityStatus.Failed && eventType != "brain.capability.denied") MarkFailure(activity);
        queue.Write(sanitizer.Sanitize(new(Guid.NewGuid(), activity.TraceId.ToHexString(), activity.SpanId.ToHexString(),
            Parent(activity), owner, eventType, activity.OperationName, status, time.GetUtcNow(), sourceEntityId, targetEntityId)));
    }

    public static string? Owner(Activity? activity)
    {
        for (var current = activity; current is not null; current = current.Parent)
            if (current.GetCustomProperty("brain.owner") is string owner) return owner;
        return null;
    }

    public static string? Parent(Activity activity) =>
        activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString();

    public static void MarkFailure(Activity? activity)
    {
        for (var current = activity; current is not null; current = current.Parent)
            if (current.GetTagItem("brain.root") is true) current.SetTag("brain.child_failed", true);
    }

    private sealed class Scope(BrainActivityPublisher publisher, Activity activity, ActivityOperation operation,
        string owner, bool root) : IActivityScope
    {
        private Exception? error;
        private bool cancelled;
        private bool disposed;
        public void Fail(Exception failure) { error = failure; MarkFailure(activity); }
        public void Cancel() => cancelled = true;
        public void Emit(string eventType, ActivityStatus status) => publisher.queue.Write(publisher.sanitizer.Sanitize(
            new(Guid.NewGuid(), activity.TraceId.ToHexString(), activity.SpanId.ToHexString(), Parent(activity), owner,
                eventType, activity.OperationName, status, publisher.time.GetUtcNow(),
                operation.SourceEntityId, operation.TargetEntityId,
                status == ActivityStatus.Started ? null : activity.Duration.TotalMilliseconds, root,
                error?.GetType().Name, error is null ? null : "Operation failed.", operation.Metadata)));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failed = error is not null || activity.GetTagItem("brain.child_failed") is true;
            activity.SetStatus(failed ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            activity.Stop();
            Emit(failed ? operation.FailedEvent : operation.CompletedEvent,
                failed ? ActivityStatus.Failed : cancelled ? ActivityStatus.Cancelled : ActivityStatus.Succeeded);
        }
    }
}

// Native HttpClient activities supply W3C propagation even without an OTLP collector.
public sealed class OutboundActivityObserver(ActivityEventQueue queue, ActivitySanitizer sanitizer, TimeProvider time)
    : IHostedService, IDisposable
{
    private ActivityListener? listener;
    public Task StartAsync(CancellationToken ct)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Emit(activity, false),
            ActivityStopped = activity => Emit(activity, true)
        };
        ActivitySource.AddActivityListener(listener);
        return Task.CompletedTask;
    }

    private void Emit(Activity activity, bool completed)
    {
        var origin = activity.Parent;
        while (origin is not null && origin.GetTagItem("brain.instance_id") is null) origin = origin.Parent;
        if (origin?.GetTagItem("brain.instance_id") as string != queue.InstanceId) return;
        if (BrainActivityPublisher.Owner(activity) is not { } owner) return;
        var statusCode = activity.GetTagItem("http.response.status_code");
        var failed = completed && (activity.Status == ActivityStatusCode.Error ||
            statusCode is int code && code >= 400 || activity.GetTagItem("error.type") is not null);
        if (failed) BrainActivityPublisher.MarkFailure(activity);
        var tags = activity.TagObjects.Where(t => t.Value is not null)
            .Select(t => new KeyValuePair<string, string>(t.Key, t.Value!.ToString()!));
        // Associate an external request with the nearest tool/service span; never record its URL.
        var parent = activity.Parent;
        var source = parent?.GetTagItem("brain.target_entity_id") as string ??
            parent?.GetTagItem("brain.source_entity_id") as string;
        queue.Write(sanitizer.Sanitize(new(Guid.NewGuid(), activity.TraceId.ToHexString(), activity.SpanId.ToHexString(),
            BrainActivityPublisher.Parent(activity), owner,
            !completed ? "external.request.started" : failed ? "external.request.failed" : "external.request.completed",
            "http.request", !completed ? ActivityStatus.Started : failed ? ActivityStatus.Failed : ActivityStatus.Succeeded,
            time.GetUtcNow(), source, FindService(activity), completed ? activity.Duration.TotalMilliseconds : null,
            ErrorType: failed ? "HttpRequestFailure" : null, Metadata: sanitizer.Metadata(tags))));
    }

    private static string? FindService(Activity activity)
    {
        for (var parent = activity.Parent; parent is not null; parent = parent.Parent)
            if (parent.GetTagItem("brain.target_entity_id") is string id && id.StartsWith("service:", StringComparison.Ordinal))
                return id;
        return null;
    }

    public Task StopAsync(CancellationToken ct) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { listener?.Dispose(); listener = null; }
}

public sealed class ActivityDispatchService(ActivityEventQueue queue, IServiceScopeFactory scopes,
    ActivityLiveBus bus, ILogger<ActivityDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var e in queue.Reader.ReadAllAsync())
        {
            // Live delivery does not depend on retention being enabled or a retained write succeeding.
            try { await bus.PublishAsync(e, stoppingToken); }
            catch (Exception error) when (error is Npgsql.NpgsqlException or TimeoutException)
            { logger.LogError("Live activity delivery failed ({ErrorType})", error.GetType().Name); }
            for (var attempt = 0; ; attempt++)
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IActivityStore>().AppendAsync(e, stoppingToken);
                    break;
                }
                catch (Exception error) when (IsStorageFailure(error))
                {
                    if (attempt < 2 && IsTransient(error))
                    {
                        logger.LogWarning("Retrying retained activity write after a transient storage failure ({ErrorType})", error.GetType().Name);
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), stoppingToken);
                        continue;
                    }
                    logger.LogError("Retained activity write failed ({ErrorType}); trace history may be incomplete", error.GetType().Name);
                    break;
                }
        }
    }

    private static bool IsStorageFailure(Exception error) =>
        error is Npgsql.NpgsqlException or Microsoft.EntityFrameworkCore.DbUpdateException or TimeoutException ||
        error is InvalidOperationException { InnerException: Npgsql.NpgsqlException };

    private static bool IsTransient(Exception error) =>
        error is Npgsql.NpgsqlException { IsTransient: true } ||
        error.InnerException is Npgsql.NpgsqlException { IsTransient: true };

    public override async Task StopAsync(CancellationToken ct)
    {
        queue.Complete();
        if (ExecuteTask is not null) await ExecuteTask.WaitAsync(ct);
        await base.StopAsync(ct);
    }
}
