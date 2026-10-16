using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brain.Infrastructure;

public static class WorkerDatabaseReadiness
{
    public static async Task<bool> TryPrepareAsync(BrainDbContext db, CancellationToken ct = default)
    {
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var vectorExists = await db.Database.SqlQueryRaw<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') AS \"Value\"").SingleAsync(ct);
            if (!vectorExists) return false;
            // A connection opened before API migrations may have cached a catalog without pgvector.
            // This refreshes this connection and invalidates the pool's cache for subsequently opened connections.
            await ((NpgsqlConnection)db.Database.GetDbConnection()).ReloadTypesAsync(ct);
            return true;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
