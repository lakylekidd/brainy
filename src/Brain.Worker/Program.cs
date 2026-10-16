using Brain.Application;
using Brain.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBrain(builder.Configuration);
builder.Services.AddKnowledgeWriter();
builder.Services.AddHostedService<DatabaseStartup>();
builder.Services.AddHostedService<ExpiredMemoryCleanup>();
builder.Services.AddHostedService<KnowledgeIndexing>();
builder.Services.AddHostedService<KnowledgeWriteProcessing>();
builder.Services.AddHostedService<RegistryIndexing>();
builder.Services.AddHostedService<MetadataScanning>();
await builder.Build().RunAsync();

public sealed class MetadataScanning(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<MetadataScanning> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Metadata:Enabled"))
        { logger.LogInformation("Metadata scanning is disabled; enable Metadata:Enabled for pending-review proposals"); return; }
        var seconds = configuration.GetValue("Worker:MetadataScanIntervalSeconds", 300);
        if (seconds < 1) throw new InvalidOperationException("Metadata scan interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<MetadataOptions>();
                var result = await scope.ServiceProvider.GetRequiredService<MetadataService>().ScanCoreAsync(options.AgentId, stoppingToken);
                logger.LogInformation("Metadata scan: {Inspected} inspected, {Skipped} skipped, {Proposed} pending proposals, {Failed} failed, busy={Busy}",
                    result.Inspected, result.Skipped, result.Proposed, result.Failed, result.AlreadyRunning);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Metadata scan failed; retrying at the next interval"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class DatabaseStartup(IServiceScopeFactory scopes, ILogger<DatabaseStartup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var waiting = false;
        while (true)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (await WorkerDatabaseReadiness.TryPrepareAsync(scope.ServiceProvider.GetRequiredService<BrainDbContext>(), cancellationToken))
                    return;
            }
            catch (Npgsql.NpgsqlException ex) { logger.LogWarning(ex, "Database is not ready for background jobs"); }
            if (!waiting) { logger.LogInformation("Waiting for operator/API migrations and pgvector before starting background jobs"); waiting = true; }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class RegistryIndexing(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<RegistryIndexing> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Registry:RootPath"]))
        { logger.LogInformation("Registry indexing is disabled because no registry root is configured"); return; }
        var seconds = configuration.GetValue("Worker:RegistryScanIntervalSeconds", 60);
        if (seconds < 1) throw new InvalidOperationException("Registry scan interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<RegistryService>().ScanCoreAsync("brain-worker", stoppingToken);
                logger.LogInformation("Registry scan: {Indexed} indexed, {Unchanged} unchanged, {Removed} removed, busy={Busy}",
                    result.Indexed, result.Unchanged, result.Removed, result.AlreadyRunning);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Registry indexing failed; retrying at the next interval"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class ExpiredMemoryCleanup(IServiceScopeFactory scopes, TimeProvider time,
    IConfiguration configuration, ILogger<ExpiredMemoryCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue("Worker:CleanupIntervalSeconds", 300);
        if (seconds < 1) throw new InvalidOperationException("Cleanup interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<IMemoryStore>().DeleteExpiredAsync(time.GetUtcNow(), stoppingToken);
                if (count > 0) logger.LogInformation("Removed {Count} expired memories", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Expired-memory cleanup failed; retrying at the next interval"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class KnowledgeIndexing(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<KnowledgeIndexing> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Knowledge:VaultPath"]))
        { logger.LogInformation("Knowledge indexing is disabled because no vault is configured"); return; }
        var seconds = configuration.GetValue("Worker:KnowledgeScanIntervalSeconds", 60);
        if (seconds < 1) throw new InvalidOperationException("Knowledge scan interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<KnowledgeService>().ScanCoreAsync("brain-worker", stoppingToken);
                logger.LogInformation("Knowledge scan: {Indexed} indexed, {Unchanged} unchanged, {Removed} removed, {Failed} failed, busy={Busy}",
                    result.Indexed, result.Unchanged, result.Removed, result.Failures.Count, result.AlreadyRunning);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Knowledge indexing failed; retrying at the next interval"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class KnowledgeWriteProcessing(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<KnowledgeWriteProcessing> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Knowledge:WriterEnabled"))
        { logger.LogInformation("Knowledge writer is disabled; approved proposals stay queued"); return; }
        var seconds = configuration.GetValue("Worker:WriteQueueIntervalSeconds", 10);
        if (seconds < 1) throw new InvalidOperationException("Write queue interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<KnowledgeWriterService>().ProcessAsync(ct: stoppingToken);
                if (result.Applied + result.Conflicts + result.Rejected + result.Deferred > 0)
                    logger.LogInformation("Knowledge writer: {Applied} applied, {Conflicts} conflicts, {Rejected} rejected, {Deferred} deferred",
                        result.Applied, result.Conflicts, result.Rejected, result.Deferred);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Knowledge writer failed; prepared proposals will be recovered on retry"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
