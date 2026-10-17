using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Brain.Application;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Brain.Infrastructure;

// PostgreSQL notifications bridge separate API/worker processes without retaining live telemetry.
public sealed class ActivityLiveBus(NpgsqlDataSource dataSource, ActivityTracingOptions options,
    ILogger<ActivityLiveBus> logger, ActivitySanitizer sanitizer) : IActivityStream
{
    internal static readonly JsonSerializerOptions Json = CreateJson();
    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false));
        return json;
    }

    public async Task PublishAsync(ActivityEvent e, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(sanitizer.Sanitize(e), Json);
        // Sanitized identifiers and bounded, allowlisted metadata keep notifications below 8KB.
        if (System.Text.Encoding.UTF8.GetByteCount(payload) >= 7900)
            throw new InvalidOperationException("Activity notification exceeded its payload bound.");
        await using var command = dataSource.CreateCommand("SELECT pg_notify('brain_activity', $1)");
        command.Parameters.AddWithValue(payload);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IActivitySubscription> SubscribeAsync(ActivityFilter filter, string? owner, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("LISTEN brain_activity", connection);
            await command.ExecuteNonQueryAsync(ct);
            return new Subscription(connection, filter, owner, options.SubscriberCapacity, logger);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private sealed class Subscription : IActivitySubscription
    {
        private readonly NpgsqlConnection connection;
        private readonly Channel<ActivityEvent> events;
        private readonly CancellationTokenSource stop = new();
        private readonly Task pump;
        private int disposed;
        public Subscription(NpgsqlConnection connection, ActivityFilter filter, string? owner, int capacity,
            ILogger logger)
        {
            this.connection = connection;
            events = Channel.CreateBounded<ActivityEvent>(new BoundedChannelOptions(capacity)
            { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
            connection.Notification += (_, notification) =>
            {
                ActivityEvent? e;
                try { e = JsonSerializer.Deserialize<ActivityEvent>(notification.Payload, Json); }
                catch (JsonException) { logger.LogWarning("Rejected a malformed activity notification"); return; }
                if (e is not null && ActivityFiltering.Matches(e, filter, owner) && !events.Writer.TryWrite(e))
                {
                    events.Writer.TryComplete(new IOException("Activity subscriber fell behind; reconnect and query retained traces."));
                    stop.Cancel();
                }
            };
            pump = PumpAsync();
        }

        private async Task PumpAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested) await connection.WaitAsync(stop.Token);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (NpgsqlException)
            { events.Writer.TryComplete(new IOException("Activity notification connection interrupted; reconnect.")); }
            finally { events.Writer.TryComplete(); }
        }

        public async IAsyncEnumerable<ActivityEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var e in events.Reader.ReadAllAsync(ct)) yield return e;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            await stop.CancelAsync();
            await pump;
            await connection.DisposeAsync();
            stop.Dispose();
        }
    }
}
