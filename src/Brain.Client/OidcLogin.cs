using System.Diagnostics;
using System.Net.Http.Json;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brain.Client;

public sealed record ClientDiscovery(string Authority, string ClientId, string Scope);

public static class OidcLogin
{
    public static async Task<string> AuthenticateAsync(HttpClient http, Uri server, bool headless,
        CancellationToken ct)
    {
        using var configResponse = await http.GetAsync(new Uri(server, "/.well-known/brain-client"), ct);
        configResponse.EnsureSuccessStatusCode();
        var config = await configResponse.Content.ReadFromJsonAsync<ClientDiscovery>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Brain did not provide client authentication settings.");
        var authority = new Uri(config.Authority.TrimEnd('/') + "/");
        if (authority.Scheme != "https" && !authority.IsLoopback)
            throw new InvalidOperationException("The identity provider must use HTTPS.");
        using var discoveryResponse = await http.GetAsync(new Uri(authority, ".well-known/openid-configuration"), ct);
        discoveryResponse.EnsureSuccessStatusCode();
        using var discovery = await JsonDocument.ParseAsync(await discoveryResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = discovery.RootElement;
        var tokenEndpoint = Endpoint(root, "token_endpoint");
        if (headless) return await DeviceCodeAsync(http, config, Endpoint(root, "device_authorization_endpoint"), tokenEndpoint, ct);
        return await BrowserAsync(http, config, Endpoint(root, "authorization_endpoint"), tokenEndpoint, ct);
    }

    private static Uri Endpoint(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || !Uri.TryCreate(element.GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" && !uri.IsLoopback)
            throw new InvalidOperationException($"OIDC discovery lacks a secure {name}.");
        return uri;
    }

    private static async Task<string> BrowserAsync(HttpClient http, ClientDiscovery config,
        Uri authorize, Uri tokenEndpoint, CancellationToken ct)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirect = $"http://127.0.0.1:{port}/callback";
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var uri = new UriBuilder(authorize);
        uri.Query = Form(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = config.ClientId, ["redirect_uri"] = redirect,
            ["scope"] = config.Scope, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["state"] = state
        });
        Process.Start(new ProcessStartInfo(uri.Uri.AbsoluteUri) { UseShellExecute = true });
        Console.WriteLine("Complete sign-in in your browser.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var client = await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var line = await reader.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("Empty OIDC callback.");
        var target = line.Split(' ', 3);
        if (target.Length != 3 || target[0] != "GET" || !target[1].StartsWith("/callback?", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid OIDC callback.");
        var values = ParseQuery(target[1]);
        values.TryGetValue("code", out var code);
        var valid = values.TryGetValue("state", out var incomingState) && incomingState == state &&
            !string.IsNullOrWhiteSpace(code);
        var body = Encoding.UTF8.GetBytes(valid ? "Brain login complete. You may close this tab." : "Brain login failed.");
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, ct);
        await stream.WriteAsync(body, ct);
        if (!valid) throw new InvalidOperationException("OIDC callback did not match the login request.");
        return await TokenAsync(http, tokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code!, ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier, ["client_id"] = config.ClientId
        }, ct);
    }

    private static async Task<string> DeviceCodeAsync(HttpClient http, ClientDiscovery config,
        Uri deviceEndpoint, Uri tokenEndpoint, CancellationToken ct)
    {
        using var response = await http.PostAsync(deviceEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = config.ClientId, ["scope"] = config.Scope }), ct);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = document.RootElement;
        var code = root.GetProperty("device_code").GetString()!;
        var userCode = root.GetProperty("user_code").GetString();
        var verification = root.TryGetProperty("verification_uri_complete", out var complete)
            ? complete.GetString() : root.GetProperty("verification_uri").GetString();
        Console.WriteLine($"Visit {verification} and enter code {userCode}.");
        var interval = root.TryGetProperty("interval", out var intervalJson) ? intervalJson.GetInt32() : 5;
        var expires = root.GetProperty("expires_in").GetInt32();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(expires);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            using var tokenResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(
                new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = code, ["client_id"] = config.ClientId }), ct);
            using var tokenDocument = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (tokenResponse.IsSuccessStatusCode) return tokenDocument.RootElement.GetProperty("access_token").GetString()!;
            var error = tokenDocument.RootElement.GetProperty("error").GetString();
            if (error == "slow_down") { interval += 5; continue; }
            if (error != "authorization_pending") throw new InvalidOperationException($"OIDC device login failed: {error}.");
        }
        throw new TimeoutException("OIDC device login expired.");
    }

    private static async Task<string> TokenAsync(HttpClient http, Uri endpoint, Dictionary<string, string> fields,
        CancellationToken ct)
    {
        using var response = await http.PostAsync(endpoint, new FormUrlEncodedContent(fields), ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("OIDC did not return an access token.");
    }

    private static string Form(Dictionary<string, string> fields) => string.Join('&',
        fields.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
    private static Dictionary<string, string> ParseQuery(string target) => target[(target.IndexOf('?') + 1)..]
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(part => part.Length == 2)
        .ToDictionary(part => Uri.UnescapeDataString(part[0]), part => Uri.UnescapeDataString(part[1]));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
