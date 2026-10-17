using Brain.Domain;

namespace Brain.Application;

public sealed class ActivityTracingOptions
{
    public int RetentionDays { get; set; } = 7;
    public int CleanupIntervalSeconds { get; set; } = 3600;
    public int QueueCapacity { get; set; } = 4096;
    public int SubscriberCapacity { get; set; } = 256;
    public string? OtlpEndpoint { get; set; }
}

public sealed record ActivityEvent(Guid Id, string TraceId, string SpanId, string? ParentSpanId,
    string AgentId, string EventType, string Action, ActivityStatus Status, DateTimeOffset Timestamp,
    string? SourceEntityId = null, string? TargetEntityId = null, double? DurationMs = null,
    bool IsRoot = false, string? ErrorType = null, string? ErrorMessage = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ActivityOperation(string Action, string StartedEvent, string CompletedEvent, string FailedEvent,
    string? SourceEntityId = null, string? TargetEntityId = null, string? AgentId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IActivityScope : IDisposable
{
    void Fail(Exception error);
    void Cancel();
}

public interface IActivityPublisher
{
    IActivityScope Start(ActivityOperation operation);
    void Publish(string eventType, ActivityStatus status, string? sourceEntityId = null, string? targetEntityId = null);
}

public static class ActivityPublisherExtensions
{
    public static async Task<T> RunAsync<T>(this IActivityPublisher publisher, ActivityOperation operation,
        Func<Task<T>> execute, CancellationToken ct = default)
    {
        using var scope = publisher.Start(operation);
        try { return await execute(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { scope.Cancel(); throw; }
        catch (Exception error) { scope.Fail(error); throw; }
    }
}

public sealed record ActivityFilter(string? Agent = null, string? Trace = null, string? Entity = null,
    string? EventType = null, ActivityStatus? Status = null, DateTimeOffset? From = null,
    DateTimeOffset? To = null, string? Tool = null, string? Skill = null, string? Service = null, int Limit = 100);

public sealed record TraceView(string TraceId, string AgentId, string RootSpanId, string RootAction,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, ActivityStatus Status, double? DurationMs);

public interface IActivityStore
{
    Task AppendAsync(ActivityEvent activity, CancellationToken ct);
    Task<IReadOnlyList<TraceView>> ListAsync(ActivityFilter filter, string? owner, CancellationToken ct);
    Task<IReadOnlyList<ActivityEvent>> EventsAsync(ActivityFilter filter, string? owner, CancellationToken ct);
    Task<int> DeleteBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}

public interface IActivitySubscription : IAsyncDisposable
{
    IAsyncEnumerable<ActivityEvent> ReadAllAsync(CancellationToken ct);
}

public interface IActivityStream
{
    Task<IActivitySubscription> SubscribeAsync(ActivityFilter filter, string? owner, CancellationToken ct);
}

public sealed class ActivityQueryService(IActivityStore store, IActivityStream stream)
{
    public Task<IReadOnlyList<TraceView>> ListAsync(AgentContext actor, ActivityFilter filter, CancellationToken ct) =>
        store.ListAsync(Validate(filter), Owner(actor, Permissions.TracesRead), ct);

    public async Task<TraceView> GetAsync(AgentContext actor, string traceId, CancellationToken ct)
    {
        var traces = await ListAsync(actor, new(Trace: traceId), ct);
        return traces.FirstOrDefault() ?? throw new BrainException(404, "Trace not found.");
    }

    public async Task<IReadOnlyList<ActivityEvent>> EventsAsync(AgentContext actor, string traceId, ActivityFilter filter, CancellationToken ct)
    {
        var owner = Owner(actor, Permissions.TracesRead);
        var validated = Validate(filter with { Trace = traceId });
        if ((await store.ListAsync(new(Trace: traceId), owner, ct)).Count == 0)
            throw new BrainException(404, "Trace not found.");
        return await store.EventsAsync(validated, owner, ct);
    }

    public Task<IActivitySubscription> SubscribeAsync(AgentContext actor, ActivityFilter filter, CancellationToken ct)
    {
        actor.Require(Permissions.ActivityRead);
        return stream.SubscribeAsync(Validate(filter), Owner(actor, Permissions.ActivityLive), ct);
    }

    public static string? Owner(AgentContext actor, string permission)
    {
        actor.Require(permission);
        return actor.Has(Permissions.TracesReadAll) ? null : actor.Id;
    }

    public static ActivityFilter Validate(ActivityFilter filter)
    {
        Validation.Require(filter.Limit is >= 1 and <= 1000, "Limit must be between 1 and 1000.");
        Validation.Require(filter.Status is null || Enum.IsDefined(filter.Status.Value), "Unknown activity status.");
        Validation.Require(filter.From is null || filter.To is null || filter.From <= filter.To, "Invalid time range.");
        foreach (var text in new[] { filter.Agent, filter.Entity, filter.EventType, filter.Tool, filter.Skill, filter.Service })
            if (text is not null) Validation.Text(text, "Activity filter", 200);
        if (filter.Trace is not null)
            Validation.Require(filter.Trace.Length == 32 && filter.Trace.All(Uri.IsHexDigit) &&
                filter.Trace.Any(c => c != '0'), "Invalid W3C trace ID.");
        return filter with { Trace = filter.Trace?.ToLowerInvariant() };
    }
}

public static class ActivityFiltering
{
    public static bool Matches(ActivityEvent e, ActivityFilter f, string? owner) =>
        (owner is null || e.AgentId == owner) && (f.Agent is null || e.AgentId == f.Agent) &&
        (f.Trace is null || e.TraceId == f.Trace) && (f.EventType is null || e.EventType == f.EventType) &&
        (f.Status is null || e.Status == f.Status) && (f.From is null || e.Timestamp >= f.From) &&
        (f.To is null || e.Timestamp <= f.To) &&
        (f.Entity is null || e.SourceEntityId == f.Entity || e.TargetEntityId == f.Entity) &&
        Entity(e, "tool", f.Tool) && Entity(e, "skill", f.Skill) && Entity(e, "service", f.Service);

    private static bool Entity(ActivityEvent e, string kind, string? id) => id is null ||
        e.SourceEntityId == $"{kind}:{id}" || e.TargetEntityId == $"{kind}:{id}";
}
