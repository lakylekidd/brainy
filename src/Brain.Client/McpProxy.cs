using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Brain.Client;

public static class McpProxy
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        var state = await ClientState.LoadAsync(ct) ?? throw new InvalidOperationException("Brain client is not registered.");
        var credential = await SecureCredentialStore.LoadAsync(ct)
            ?? throw new InvalidOperationException("Brain device credential is unavailable.");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var protocolVersion = "2025-11-25";
        while (await Console.In.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? id = null;
            try
            {
                using var request = JsonDocument.Parse(line);
                if (request.RootElement.TryGetProperty("id", out var idValue)) id = idValue.GetRawText();
                if (request.RootElement.TryGetProperty("method", out var method) &&
                    method.ValueKind == JsonValueKind.String && method.GetString() == "initialize" &&
                    request.RootElement.TryGetProperty("params", out var parameters) &&
                    parameters.ValueKind == JsonValueKind.Object &&
                    parameters.TryGetProperty("protocolVersion", out var requestedVersion) &&
                    requestedVersion.ValueKind == JsonValueKind.String &&
                    requestedVersion.GetString() is { } version &&
                    System.Text.RegularExpressions.Regex.IsMatch(version, "^[0-9]{4}-[0-9]{2}-[0-9]{2}$"))
                    protocolVersion = version;
                using var outbound = new HttpRequestMessage(HttpMethod.Post, new Uri(state.Server, "/mcp"));
                outbound.Headers.Add("MCP-Protocol-Version", protocolVersion);
                outbound.Content = new StringContent(line, Encoding.UTF8, "application/json");
                using var response = await http.SendAsync(outbound, ct);
                if (!response.IsSuccessStatusCode)
                {
                    if (id is not null) await Console.Out.WriteLineAsync(Error(id, $"Brain MCP returned HTTP {(int)response.StatusCode}."));
                    continue;
                }
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
                {
                    foreach (var value in body.Split('\n').Where(x => x.StartsWith("data:", StringComparison.Ordinal)))
                        await Console.Out.WriteLineAsync(value[5..].Trim());
                }
                else if (!string.IsNullOrWhiteSpace(body)) await Console.Out.WriteLineAsync(body);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (id is not null) await Console.Out.WriteLineAsync(Error(id, "Brain MCP request failed."));
                Console.Error.WriteLine(error.GetType().Name);
            }
        }
    }

    private static string Error(string id, string message) =>
        $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":-32000,\"message\":{JsonSerializer.Serialize(message)}}}}}";
}
