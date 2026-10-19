using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Brain.Client;

return await ClientProgram.RunAsync(args);

namespace Brain.Client
{

public static class ClientProgram
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            if (args.Length == 0) { Help(); return 2; }
            if (args is ["mcp-proxy"]) { await McpProxy.RunAsync(ct); return 0; }
            if (args is ["agent", "run"]) { await AgentLoop.RunAsync(ct); return 0; }
            if (args is ["install"]) { await ServiceInstaller.InstallAsync(ct); return 0; }
            if (args is ["uninstall"]) { await ServiceInstaller.UninstallAsync(ct); return 0; }
            if (args[0] == "login") { await LoginAsync(args, http, ct); return 0; }
            var state = await ClientState.LoadAsync(ct);
            if (args[0] == "logout") { await LogoutAsync(state, http, ct); return 0; }
            if (state is null) throw new InvalidOperationException("Run brain login --server <URL> first.");
            var credential = await SecureCredentialStore.LoadAsync(ct)
                ?? throw new InvalidOperationException("Device credential missing from the OS credential store.");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            switch (args)
            {
                case ["status"]:
                    Print(new { device = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}", ct),
                        skills = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/skills", ct),
                        runtimes = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/runtime", ct) });
                    break;
                case ["devices"]: Print(await GetAsync(http, state.Server, "/api/devices", ct)); break;
                case ["skills", "list"]: Print(await GetAsync(http, state.Server, "/api/skills", ct)); break;
                case ["doctor"]: return await DoctorAsync(http, state, ct);
                case ["runtimes", "inspect"]: Print(RuntimeInventory.Inspect()); break;
                case ["runtimes", "report"]: await AgentLoop.ReportOnceAsync(http, state, ct); break;
                case ["dependencies", "check"]:
                    Print(await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/dependencies", ct)); break;
                case ["sync"] or ["skills", "sync"]: await SkillSynchronizer.SyncAsync(http, state, ct); break;
                case ["connect", var harness]:
                    await HarnessConnections.ConnectAsync(harness, state, ct);
                    await ReportHarnessesAsync(http, state, ct); break;
                case ["disconnect", var harness]:
                    await HarnessConnections.DisconnectAsync(harness, state, ct);
                    await ReportHarnessesAsync(http, state, ct); break;
                case ["heartbeat"]: await HeartbeatAsync(http, state, ct); break;
                case ["rotate"]: await RotateAsync(http, state, ct); break;
                case ["provision", var requirement]:
                    await HumanProvisionAsync(http, state, requirement, null, ct); break;
                case ["approve", var id] when Guid.TryParse(id, out var planId):
                    await HumanProvisionAsync(http, state, null, planId, ct); break;
                default: Help(); return 2;
            }
            return 0;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static async Task LoginAsync(string[] args, HttpClient http, CancellationToken ct)
    {
        var index = Array.IndexOf(args, "--server");
        if (index < 0 || index + 1 >= args.Length ||
            !Uri.TryCreate(args[index + 1], UriKind.Absolute, out var server) ||
            server.Scheme != "https" && !(server.IsLoopback && server.Scheme == "http") ||
            server.UserInfo.Length > 0 || server.Query.Length > 0 || server.Fragment.Length > 0)
            throw new InvalidOperationException("Use --server with an HTTPS URL (HTTP only on loopback).");
        if (await ClientState.LoadAsync(ct) is not null)
            throw new InvalidOperationException("Already registered. Run brain logout first.");
        var accessToken = await OidcLogin.AuthenticateAsync(http, server, args.Contains("--headless"), ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var hostname = Environment.MachineName;
        var name = new string(hostname.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (name.Length == 0) name = "computer";
        name = name[..Math.Min(name.Length, 90)] + "-" + Guid.NewGuid().ToString("N")[..8];
        var installedHarnesses = HarnessConnections.Inspect().Where(harness => harness.Installed)
            .Select(harness => harness.Name).ToArray();
        var request = new { name, displayName = hostname, hostname, operatingSystem = os,
            architecture = arch, clientVersion = typeof(ClientProgram).Assembly.GetName().Version?.ToString() ?? "dev",
            metadata = new { installedHarnesses } };
        using var response = await http.PostAsJsonAsync(new Uri(server, "/api/devices/register"), request, ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var id = json.RootElement.GetProperty("device").GetProperty("id").GetGuid();
        var credential = json.RootElement.GetProperty("credential").GetString()!;
        try
        {
            await SecureCredentialStore.SaveAsync(credential, ct);
            await new ClientState(server, id, name).SaveAsync(ct);
        }
        catch
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            try
            {
                using var revoked = await http.PostAsync(new Uri(server, $"/api/devices/{id}/revoke"), null, ct);
                revoked.EnsureSuccessStatusCode();
                await SecureCredentialStore.DeleteAsync(ct);
            }
            catch { throw new InvalidOperationException($"Local credential storage failed. Revoke device {id} from another authorized session."); }
            throw;
        }
        Console.WriteLine($"Registered {name} ({id}).");
    }

    private static async Task LogoutAsync(ClientState? state, HttpClient http, CancellationToken ct)
    {
        if (state is null) { Console.WriteLine("Already logged out."); return; }
        var credential = await SecureCredentialStore.LoadAsync(ct)
            ?? throw new InvalidOperationException("Credential missing; revoke the device from another session.");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using var response = await http.PostAsync(new Uri(state.Server, $"/api/devices/{state.DeviceId}/revoke"), null, ct);
        response.EnsureSuccessStatusCode();
        await SecureCredentialStore.DeleteAsync(ct);
        File.Delete(ClientState.FilePath);
        Console.WriteLine("Device revoked and local credential removed.");
    }

    private static async Task RotateAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        using var response = await http.PostAsync(new Uri(state.Server, $"/api/devices/{state.DeviceId}/rotate"), null, ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        await SecureCredentialStore.SaveAsync(json.RootElement.GetProperty("credential").GetString()!, ct);
        Console.WriteLine("Device credential rotated.");
    }

    private static async Task HeartbeatAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        var request = new { clientVersion = typeof(ClientProgram).Assembly.GetName().Version?.ToString() ?? "dev",
            runtimeAgentVersion = (string?)null, healthy = true,
            metadata = HarnessConnections.Metadata(state) };
        using var response = await http.PostAsJsonAsync(new Uri(state.Server, $"/api/devices/{state.DeviceId}/heartbeat"), request, ct);
        response.EnsureSuccessStatusCode();
        Console.WriteLine("Heartbeat accepted.");
    }

    private static async Task ReportHarnessesAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        try
        {
            var updated = await ClientState.LoadAsync(ct) ?? state;
            await HeartbeatAsync(http, updated, ct);
        }
        catch (HttpRequestException)
        { Console.Error.WriteLine("Harness changed locally; Brain status will update on the next heartbeat."); }
    }

    private static async Task HumanProvisionAsync(HttpClient http, ClientState state,
        string? requirement, Guid? planId, CancellationToken ct)
    {
        var token = await OidcLogin.AuthenticateAsync(http, state.Server, false, ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var path = $"/api/devices/{state.DeviceId}/provisioning";
        using var response = planId is null
            ? await http.PostAsJsonAsync(new Uri(state.Server, path), new { requirement }, ct)
            : await http.PostAsync(new Uri(state.Server, $"{path}/{planId}/approve"), null, ct);
        response.EnsureSuccessStatusCode();
        Print(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct));
    }

    private static async Task<int> DoctorAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        var failures = 0;
        foreach (var (label, path) in new[] { ("Brain API", "/health/ready"),
                     ("Device", $"/api/devices/{state.DeviceId}"), ("Skill registry", "/api/skills"),
                     ("Runtime report", $"/api/devices/{state.DeviceId}/runtime"),
                     ("Skill sync state", $"/api/devices/{state.DeviceId}/skills"),
                     ("Dependencies", $"/api/devices/{state.DeviceId}/dependencies") })
        {
            try { await GetAsync(http, state.Server, path, ct); Console.WriteLine($"{label}: OK"); }
            catch (Exception error) { failures++; Console.WriteLine($"{label}: {error.Message}"); }
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(state.Server, "/mcp"));
            request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
            request.Headers.Accept.ParseAdd("application/json, text/event-stream");
            request.Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"brain-client\",\"version\":\"1.0\"}}",
                System.Text.Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            Console.WriteLine("MCP: OK");
        }
        catch (Exception error) { failures++; Console.WriteLine($"MCP: {error.Message}"); }
        foreach (var harness in HarnessConnections.Inspect())
            Console.WriteLine($"{harness.Name}: {(harness.Installed ? "installed" : "missing")}");
        foreach (var name in state.ConnectedHarnesses)
        {
            try
            {
                await HarnessConnections.Adapters.Single(x => x.Name == name).VerifyAsync(ct);
                Console.WriteLine($"{name} MCP configuration: OK");
            }
            catch (Exception error) { failures++; Console.WriteLine($"{name} MCP configuration: {error.Message}"); }
        }
        try
        {
            var runtimes = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/runtime", ct);
            if (runtimes.GetArrayLength() == 0)
            { failures++; Console.WriteLine("Runtime Agent: no report; run brain runtimes report or brain agent run."); }
            var skills = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/skills", ct);
            foreach (var skill in skills.EnumerateArray().Where(x => x.GetProperty("status").GetString() != "synced"))
            {
                failures++;
                Console.WriteLine($"Skill {skill.GetProperty("skillId").GetString()}: {skill.GetProperty("status").GetString()}; inspect local edits.");
            }
            var dependencies = await GetAsync(http, state.Server, $"/api/devices/{state.DeviceId}/dependencies", ct);
            foreach (var item in dependencies.EnumerateArray().Where(x => !x.GetProperty("available").GetBoolean()))
            {
                failures++;
                Console.WriteLine($"Dependency {item.GetProperty("requirement").GetString()}: {item.GetProperty("reason").GetString()}.");
            }
        }
        catch (Exception error) { failures++; Console.WriteLine($"Detailed diagnostics unavailable: {error.Message}"); }
        Console.WriteLine("Runtime: " + RuntimeInformation.OSDescription);
        return failures == 0 ? 0 : 1;
    }

    private static async Task<JsonElement> GetAsync(HttpClient http, Uri server, string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri(server, path), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    private static void Print(object? value) => Console.WriteLine(JsonSerializer.Serialize(value,
        new JsonSerializerOptions { WriteIndented = true }));
    private static void Help() => Console.WriteLine("brain install | uninstall | login --server <URL> [--headless] | logout | status | devices | doctor | skills list|sync | sync | runtimes inspect|report | dependencies check | connect <claude-code|codex|hermes> | disconnect <name> | heartbeat | rotate | provision <requirement> | approve <plan-id> | agent run");
}
}
