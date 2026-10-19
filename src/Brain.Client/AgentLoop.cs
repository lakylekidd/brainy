using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Brain.Client;

public static class AgentLoop
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var state = await ClientState.LoadAsync(ct)
            ?? throw new InvalidOperationException("Run brain login before starting the Runtime Agent.");
        if (await SecureCredentialStore.LoadAsync(ct) is null)
            throw new InvalidOperationException("Device credential unavailable to the Runtime Agent identity.");
        using var syncGate = new SemaphoreSlim(1, 1);
        var events = ListenAsync(state, syncGate, ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var credential = await SecureCredentialStore.LoadAsync(ct)
                    ?? throw new InvalidOperationException("Device credential unavailable to the Runtime Agent identity.");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                var currentState = await ClientState.LoadAsync(ct) ?? state;
                try { await ReportOnceAsync(http, currentState, ct); }
                catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException)
                { Console.Error.WriteLine($"Brain Runtime Agent report: {error.Message}"); }
                try { await SyncAsync(http, currentState, syncGate, ct); }
                catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException)
                { Console.Error.WriteLine($"Brain Runtime Agent skill sync: {error.Message}"); }
                try { await Provisioner.ProcessQueueAsync(http, currentState, ct); }
                catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException)
                { Console.Error.WriteLine($"Brain Runtime Agent provisioning: {error.Message}"); }
            }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException)
            { Console.Error.WriteLine($"Brain Runtime Agent: {error.Message}"); }
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
        await events;
    }

    public static async Task ReportOnceAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        using var heartbeat = await http.PostAsJsonAsync(new Uri(state.Server, $"/api/devices/{state.DeviceId}/heartbeat"),
            new { clientVersion = typeof(AgentLoop).Assembly.GetName().Version?.ToString() ?? "dev",
                runtimeAgentVersion = typeof(AgentLoop).Assembly.GetName().Version?.ToString() ?? "dev", healthy = true,
                metadata = HarnessConnections.Metadata(state) }, ct);
        heartbeat.EnsureSuccessStatusCode();
        using var report = await http.PostAsJsonAsync(new Uri(state.Server, $"/api/devices/{state.DeviceId}/runtime"),
            await RuntimeInventory.InspectForReportAsync(ct), ct);
        report.EnsureSuccessStatusCode();
        Console.WriteLine("Host runtime reported.");
    }

    private static async Task SyncAsync(HttpClient http, ClientState state, SemaphoreSlim gate, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { await SkillSynchronizer.SyncAsync(http, state, ct); }
        finally { gate.Release(); }
    }

    private static async Task ListenAsync(ClientState state, SemaphoreSlim gate, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var credential = await SecureCredentialStore.LoadAsync(ct)
                    ?? throw new InvalidOperationException("Device credential unavailable to the Runtime Agent identity.");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                using var response = await http.GetAsync(new Uri(state.Server, "/api/client/skill-events"),
                    HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(body);
                while (await reader.ReadLineAsync(ct) is { } line)
                    if (line == "event: skill.changed")
                        await SyncAsync(http, await ClientState.LoadAsync(ct) ?? state, gate, ct);
            }
            catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException or InvalidOperationException)
            { Console.Error.WriteLine("Skill event stream disconnected; periodic sync remains active."); }
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
