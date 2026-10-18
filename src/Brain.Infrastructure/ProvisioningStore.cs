using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure;

public sealed class ProvisioningStore(BrainDbContext db, TimeProvider time) : IProvisioningStore
{
    public async Task<ProvisioningView> CreateAsync(ProvisioningPlan plan, CancellationToken ct)
    {
        db.ProvisioningPlans.Add(plan);
        Audit(plan.RequestedBy, "brain.runtime.provisioning.planned", plan.Id);
        await db.SaveChangesAsync(ct);
        return View(plan);
    }

    public async Task<ProvisioningView> ApproveAsync(Guid deviceId, Guid planId, string approvedBy, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.ProvisioningPlans.Where(x => x.Id == planId && x.DeviceId == deviceId &&
            x.Status == ProvisioningStatus.Pending).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, ProvisioningStatus.Approved)
                .SetProperty(p => p.ApprovedBy, approvedBy)
                .SetProperty(p => p.ApprovedAt, time.GetUtcNow()), ct);
        if (changed != 1) throw new BrainException(409, "Provisioning plan is no longer pending.");
        var plan = await RequireAsync(deviceId, planId, ct);
        Audit(plan.RequestedBy, "brain.runtime.provisioning.approved", planId,
            JsonSerializer.Serialize(new { approvedBy }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(plan);
    }

    public async Task<IReadOnlyList<ProvisioningView>> ListAsync(Guid deviceId, CancellationToken ct) =>
        (await db.ProvisioningPlans.AsNoTracking().Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct)).Select(View).ToArray();

    public async Task<IReadOnlyList<ProvisioningView>> QueueAsync(Guid deviceId, CancellationToken ct)
    {
        var retryBefore = time.GetUtcNow().AddMinutes(-30);
        return (await db.ProvisioningPlans.AsNoTracking().Where(x => x.DeviceId == deviceId &&
            (x.Status == ProvisioningStatus.Approved || x.Status == ProvisioningStatus.Running &&
             x.StartedAt < retryBefore)).OrderBy(x => x.CreatedAt).Take(20).ToListAsync(ct)).Select(View).ToArray();
    }

    public async Task<ProvisioningView> StartAsync(Guid deviceId, Guid planId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var retryBefore = time.GetUtcNow().AddMinutes(-30);
        var changed = await db.ProvisioningPlans.Where(x => x.Id == planId && x.DeviceId == deviceId &&
            (x.Status == ProvisioningStatus.Approved || x.Status == ProvisioningStatus.Running &&
             x.StartedAt < retryBefore)).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, ProvisioningStatus.Running)
                .SetProperty(p => p.StartedAt, time.GetUtcNow()), ct);
        if (changed != 1) throw new BrainException(409, "Provisioning plan is not available.");
        var plan = await RequireAsync(deviceId, planId, ct);
        Audit(plan.RequestedBy, "brain.runtime.provisioning.started", planId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(plan);
    }

    public async Task<ProvisioningView> CompleteAsync(Guid deviceId, Guid planId,
        ProvisionResultRequest result, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var status = result.Succeeded ? ProvisioningStatus.Completed : ProvisioningStatus.Failed;
        var changed = await db.ProvisioningPlans.Where(x => x.Id == planId && x.DeviceId == deviceId &&
            x.Status == ProvisioningStatus.Running).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.Result, result.Result)
                .SetProperty(p => p.CompletedAt, time.GetUtcNow()), ct);
        if (changed != 1) throw new BrainException(409, "Provisioning plan is not running.");
        var plan = await RequireAsync(deviceId, planId, ct);
        Audit(plan.RequestedBy, result.Succeeded ? "brain.runtime.provisioning.completed" : "brain.runtime.provisioning.failed",
            planId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(plan);
    }

    private async Task<ProvisioningPlan> RequireAsync(Guid deviceId, Guid planId, CancellationToken ct) =>
        await db.ProvisioningPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId && x.DeviceId == deviceId, ct)
            ?? throw new BrainException(404, "Provisioning plan not found.");

    private void Audit(string actor, string action, Guid id, string metadata = "{}") => db.AuditEvents.Add(new AuditEvent
    { AgentId = actor, Action = action, Resource = $"provisioning:{id}", Result = "success",
        MetadataJson = metadata, Timestamp = time.GetUtcNow() });

    private static ProvisioningView View(ProvisioningPlan x) => new(x.Id, x.DeviceId,
        x.Requirement, x.PackageId, x.PackageVersion, x.WheelUrl, x.Sha256,
        x.OperatingSystem, x.Architecture, x.Status, x.RequestedBy, x.ApprovedBy,
        x.CreatedAt, x.ApprovedAt, x.StartedAt, x.CompletedAt, x.Result);
}
