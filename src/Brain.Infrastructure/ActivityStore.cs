using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class ActivityStore(BrainDbContext db, ActivityTracingOptions options, ActivitySanitizer sanitizer) : IActivityStore
{
    public async Task AppendAsync(ActivityEvent e, CancellationToken ct)
    {
        if (options.RetentionDays == 0) return;
        e = sanitizer.Sanitize(e);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Different processes may append to the same trace. A caller cannot claim another owner's trace.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({e.TraceId + ":" + e.AgentId}, 7))", ct);
        var trace = await db.ActivityTraces.SingleOrDefaultAsync(x => x.TraceId == e.TraceId && x.AgentId == e.AgentId, ct);
        if (await db.ActivityEvents.AnyAsync(x => x.Id == e.Id, ct)) return;
        if (trace is null)
        {
            trace = new() { TraceId = e.TraceId, AgentId = e.AgentId, RootSpanId = e.SpanId,
                RootAction = e.Action, StartedAt = e.Timestamp, Status = ActivityStatus.Running };
            db.ActivityTraces.Add(trace);
        }
        if (e.IsRoot && e.Status == ActivityStatus.Started && e.Timestamp <= trace.StartedAt)
        {
            trace.RootSpanId = e.SpanId; trace.RootAction = e.Action; trace.StartedAt = e.Timestamp;
        }
        trace.HasFailures |= e.Status == ActivityStatus.Failed && e.EventType != "brain.capability.denied";
        trace.HasCancellations |= e.Status == ActivityStatus.Cancelled;
        if (e.IsRoot && e.Status == ActivityStatus.Started)
        {
            trace.ActiveRoots++;
            trace.Status = ActivityStatus.Running;
            trace.CompletedAt = null;
            trace.DurationMs = null;
        }
        if (e.IsRoot && e.Status is ActivityStatus.Succeeded or ActivityStatus.Failed or ActivityStatus.Cancelled)
        {
            trace.ActiveRoots = Math.Max(0, trace.ActiveRoots - 1);
            if (trace.ActiveRoots == 0)
            {
                trace.CompletedAt = e.Timestamp;
                trace.DurationMs = Math.Max(0, (e.Timestamp - trace.StartedAt).TotalMilliseconds);
                trace.Status = trace.HasCancellations ? ActivityStatus.Cancelled : e.Status;
            }
        }
        if (trace.HasFailures && trace.CompletedAt is not null) trace.Status = ActivityStatus.Failed;
        db.ActivityEvents.Add(new RetainedActivityEvent
        {
            Id = e.Id, TraceId = e.TraceId, AgentId = e.AgentId, SpanId = e.SpanId, ParentSpanId = e.ParentSpanId,
            SourceEntityId = e.SourceEntityId, TargetEntityId = e.TargetEntityId, EventType = e.EventType, Action = e.Action,
            Status = e.Status, Timestamp = e.Timestamp, DurationMs = e.DurationMs, IsRoot = e.IsRoot,
            ErrorType = e.ErrorType, ErrorMessage = e.ErrorMessage, MetadataJson = JsonSerializer.Serialize(e.Metadata ?? new Dictionary<string, string>())
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private IQueryable<RetainedActivityEvent> Events(ActivityFilter f, string? owner)
    {
        var q = db.ActivityEvents.AsNoTracking();
        if (owner is not null) q = q.Where(e => e.AgentId == owner);
        if (f.Agent is not null) q = q.Where(e => e.AgentId == f.Agent);
        if (f.Trace is not null) q = q.Where(e => e.TraceId == f.Trace);
        if (f.Entity is not null) q = q.Where(e => e.SourceEntityId == f.Entity || e.TargetEntityId == f.Entity);
        if (f.EventType is not null) q = q.Where(e => e.EventType == f.EventType);
        if (f.From is not null) q = q.Where(e => e.Timestamp >= f.From);
        if (f.To is not null) q = q.Where(e => e.Timestamp <= f.To);
        if (f.Tool is not null) q = q.Where(e => e.SourceEntityId == "tool:" + f.Tool || e.TargetEntityId == "tool:" + f.Tool);
        if (f.Skill is not null) q = q.Where(e => e.SourceEntityId == "skill:" + f.Skill || e.TargetEntityId == "skill:" + f.Skill);
        if (f.Service is not null) q = q.Where(e => e.SourceEntityId == "service:" + f.Service || e.TargetEntityId == "service:" + f.Service);
        return q;
    }

    public async Task<IReadOnlyList<TraceView>> ListAsync(ActivityFilter f, string? owner, CancellationToken ct)
    {
        var matching = Events(f, owner);
        var q = db.ActivityTraces.AsNoTracking().Where(t => matching.Any(e => e.TraceId == t.TraceId && e.AgentId == t.AgentId));
        if (f.Status is not null) q = q.Where(t => t.Status == f.Status);
        return await q.OrderByDescending(t => t.StartedAt).ThenBy(t => t.TraceId).ThenBy(t => t.AgentId).Take(f.Limit)
            .Select(t => new TraceView(t.TraceId, t.AgentId, t.RootSpanId, t.RootAction, t.StartedAt,
                t.CompletedAt, t.Status, t.DurationMs)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<ActivityEvent>> EventsAsync(ActivityFilter f, string? owner, CancellationToken ct)
    {
        var q = Events(f, owner);
        if (f.Status is not null) q = q.Where(e => e.Status == f.Status);
        var events = await q.OrderBy(e => e.Timestamp).ThenBy(e => e.Id).Take(f.Limit).ToArrayAsync(ct);
        return events.Select(e => new ActivityEvent(e.Id, e.TraceId, e.SpanId, e.ParentSpanId, e.AgentId,
            e.EventType, e.Action, e.Status, e.Timestamp, e.SourceEntityId, e.TargetEntityId, e.DurationMs,
            e.IsRoot, e.ErrorType, e.ErrorMessage, JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson))).ToArray();
    }

    public Task<int> DeleteBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        db.ActivityTraces.Where(t => (t.CompletedAt ?? t.StartedAt) < cutoff &&
            !db.ActivityEvents.Any(e => e.TraceId == t.TraceId && e.AgentId == t.AgentId && e.Timestamp >= cutoff))
            .ExecuteDeleteAsync(ct);
}
