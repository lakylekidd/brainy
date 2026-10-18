using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Brain.IntegrationTests;

public sealed class WorkerReadinessTests(BrainFixture fixture) : IClassFixture<BrainFixture>
{
    [Fact]
    public async Task Worker_waits_for_migrations_and_refreshes_a_type_cache_opened_before_pgvector_exists()
    {
        var parent = fixture.ConnectionString;
        var database = "brain_readiness_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(parent);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var workerConnection = new NpgsqlConnectionStringBuilder(parent) { Database = database, ApplicationName = "worker-readiness-test" }.ConnectionString;
            var migrationConnection = new NpgsqlConnectionStringBuilder(parent) { Database = database, ApplicationName = "api-migration-test" }.ConnectionString;
            await using var workerDb = Context(workerConnection);
            Assert.False(await WorkerDatabaseReadiness.TryPrepareAsync(workerDb));
            await using (var migrationDb = Context(migrationConnection)) await migrationDb.Database.MigrateAsync();
            Assert.True(await WorkerDatabaseReadiness.TryPrepareAsync(workerDb));
            var provider = new DevelopmentEmbeddingProvider();
            var definition = new RegistryDefinition(RegistryKind.Tool, "readiness", "Readiness", "Verify vector writes",
                [], [], [], "mcp", ["verify"], "tools/readiness/manifest.yaml", new string('a', 64));
            await new RegistryIndex(workerDb).ReplaceAsync([new(definition, await provider.EmbedAsync("verify", default))],
                provider.Model, DateTimeOffset.UtcNow,
                new AuditEvent { AgentId = "test", Action = "test", Resource = "readiness" }, default);
            Assert.Equal(384, (await workerDb.RegistryEntries.SingleAsync()).Embedding.ToArray().Length);
        }
        finally
        {
            // This test generated the database identifier; no parent database tables are changed.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static BrainDbContext Context(string connection) => new(new DbContextOptionsBuilder<BrainDbContext>()
        .UseNpgsql(connection, o => o.UseVector()).Options);
}
