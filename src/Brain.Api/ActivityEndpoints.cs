using System.Diagnostics;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Brain.Api;

public sealed record ActivityParameters(string? Agent = null, string? Trace = null, string? Entity = null,
    string? EventType = null, string? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null,
    string? Tool = null, string? Skill = null, string? Service = null, int Limit = 100)
{
    public ActivityFilter Filter()
    {
        ActivityStatus? status = null;
        if (Status is not null)
        {
            if (Status.Length == 0 || !Status.All(char.IsAsciiLetter) ||
                !Enum.TryParse<ActivityStatus>(Status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new BrainException(400, "Unknown activity status.");
            status = parsed;
        }
        return new(Agent, Trace, Entity, EventType, status, From, To, Tool, Skill, Service, Limit);
    }
}

public static class ActivityEndpoints
{
    public static void UseBrainRequestActivity(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.Request.Path.StartsWithSegments("/api/activity") ||
            context.Request.Path.StartsWithSegments("/api/traces") ||
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/openapi"))
        { await next(context); return; }
        var actor = context.RequestServices.GetRequiredService<RequestAgent>().Context;
        var publisher = context.RequestServices.GetRequiredService<IActivityPublisher>();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "request";
        using var activity = publisher.Start(new($"{context.Request.Method} {route}",
            "agent.request.started", "agent.request.completed", "agent.request.failed",
            $"agent:{actor.Id}", "brain", actor.Id));
        context.Response.Headers["X-Trace-Id"] = Activity.Current!.TraceId.ToHexString();
        try
        {
            await next(context);
            if (context.Response.StatusCode >= 400) activity.Fail(new BrainException(context.Response.StatusCode, "Request failed."));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { activity.Cancel(); throw; }
        catch (Exception error) { activity.Fail(error); throw; }
    });

    public static void MapBrainActivity(this WebApplication app)
    {
        app.MapGet("/api/traces", ([AsParameters] ActivityParameters parameters, RequestAgent agent,
            ActivityQueryService query, CancellationToken ct) => query.ListAsync(agent.Context, parameters.Filter(), ct));
        app.MapGet("/api/traces/{traceId}", (string traceId, RequestAgent agent,
            ActivityQueryService query, CancellationToken ct) => query.GetAsync(agent.Context, traceId, ct));
        app.MapGet("/api/traces/{traceId}/events", (string traceId, [AsParameters] ActivityParameters parameters,
            RequestAgent agent, ActivityQueryService query, CancellationToken ct) => query.EventsAsync(agent.Context, traceId, parameters.Filter(), ct));
        app.MapGet("/api/activity/stream", StreamAsync);
    }

    private static async Task StreamAsync(HttpContext context, [AsParameters] ActivityParameters parameters, RequestAgent agent,
        ActivityQueryService query, IServiceScopeFactory scopes, TimeProvider time, CancellationToken ct)
    {
        await using var subscription = await query.SubscribeAsync(agent.Context, parameters.Filter(), ct);
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await context.Response.WriteAsync("retry: 3000\n: connected\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false));
        using var readStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var enumerator = subscription.ReadAllAsync(readStop.Token).GetAsyncEnumerator(readStop.Token);
        var pending = enumerator.MoveNextAsync().AsTask();
        var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), ct);
        var checkAt = time.GetUtcNow().AddSeconds(15);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.WhenAny(pending, heartbeat);
                if (time.GetUtcNow() >= checkAt)
                {
                    using var scope = scopes.CreateScope();
                    var current = await scope.ServiceProvider.GetRequiredService<IAgentDirectory>().ResolveAsync(agent.Context.Id, ct);
                    if (current is null || !current.Has(Permissions.ActivityRead) || !current.Has(Permissions.ActivityLive) ||
                        current.Has(Permissions.TracesReadAll) != agent.Context.Has(Permissions.TracesReadAll))
                    {
                        await context.Response.WriteAsync("event: stream.error\ndata: {\"error\":\"Access changed; reconnect.\"}\n\n", ct);
                        break;
                    }
                    checkAt = time.GetUtcNow().AddSeconds(15);
                    heartbeat = Task.Delay(TimeSpan.FromSeconds(15), ct);
                    await context.Response.WriteAsync(": heartbeat\n\n", ct);
                }
                if (pending.IsCompleted)
                {
                    if (!await pending) break;
                    var e = enumerator.Current;
                    await context.Response.WriteAsync($"id: {e.Id}\nevent: activity\ndata: {JsonSerializer.Serialize(e, json)}\n\n", ct);
                    pending = enumerator.MoveNextAsync().AsTask();
                }
                await context.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            await context.Response.WriteAsync("event: stream.error\ndata: {\"error\":\"Activity delivery interrupted; reconnect and query traces.\"}\n\n", ct);
        }
        finally
        {
            // Stop a pending channel read before disposing its async enumerator.
            await readStop.CancelAsync();
            try { await pending; }
            catch (OperationCanceledException) when (readStop.IsCancellationRequested) { }
            catch (IOException) { }
        }
    }
}
