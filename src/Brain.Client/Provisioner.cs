using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brain.Client;

public static class Provisioner
{
    private static readonly Regex Requirement = new("^host:([a-z][a-z0-9_.-]{0,99})(?:>=[0-9.]+)?$", RegexOptions.Compiled);
    private static readonly Regex Package = new("^[a-z0-9][a-z0-9._-]{0,99}$", RegexOptions.Compiled);

    public static async Task ProcessQueueAsync(HttpClient api, ClientState state, CancellationToken ct)
    {
        using var response = await api.GetAsync(new Uri(state.Server,
            $"/api/devices/{state.DeviceId}/provisioning/queue"), ct);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetGuid();
            using var started = await api.PostAsync(new Uri(state.Server,
                $"/api/devices/{state.DeviceId}/provisioning/{id}/start"), null, ct);
            if (!started.IsSuccessStatusCode) continue;
            string result;
            try { result = await ExecuteAsync(item, ct); }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException)
            { result = error is OperationCanceledException && ct.IsCancellationRequested ? "cancelled" : "provisioning failed"; }
            using var completed = await api.PostAsJsonAsync(new Uri(state.Server,
                $"/api/devices/{state.DeviceId}/provisioning/{id}/complete"),
                new { succeeded = result == "verified", result }, ct);
            completed.EnsureSuccessStatusCode();
            if (result == "verified") Console.WriteLine($"Provisioning {id}: verified.");
            else Console.Error.WriteLine($"Provisioning {id}: {result}.");
        }
    }

    private static async Task<string> ExecuteAsync(JsonElement plan, CancellationToken ct)
    {
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (plan.GetProperty("operatingSystem").GetString() != os ||
            plan.GetProperty("architecture").GetString() != arch)
            return "platform mismatch";
        var requirement = plan.GetProperty("requirement").GetString()!;
        var command = Requirement.Match(requirement);
        var package = plan.GetProperty("packageId").GetString()!;
        var version = plan.GetProperty("packageVersion").GetString()!;
        var sha = plan.GetProperty("sha256").GetString()!;
        if (!command.Success || !Package.IsMatch(package) ||
            !Regex.IsMatch(version, "^[0-9]+(?:[.][0-9]+){1,3}$") ||
            sha.Length != 64 || !sha.All(Uri.IsHexDigit)) return "invalid approved plan";
        if (await RequirementAvailableAsync(requirement, ct)) return "verified";
        if (!Uri.TryCreate(plan.GetProperty("wheelUrl").GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return "invalid artifact URL";
        var filename = Uri.UnescapeDataString(uri.Segments.Last());
        var prefix = package.Replace('-', '_') + "-" + version + "-";
        if (!filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !filename.EndsWith(".whl", StringComparison.OrdinalIgnoreCase) ||
            filename.Contains('/') || filename.Contains('\\')) return "artifact identity mismatch";
        var pipx = RuntimeInventory.FindExecutable("pipx");
        if (pipx is null) return "pipx unavailable";
        var downloadDirectory = Path.Combine(ClientState.DirectoryPath, "downloads");
        Directory.CreateDirectory(downloadDirectory);
        if (new DirectoryInfo(downloadDirectory).LinkTarget is not null) return "unsafe download directory";
        var temporaryDirectory = Path.Combine(downloadDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var file = Path.Combine(temporaryDirectory, filename);
        try
        {
            using var downloader = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { Timeout = TimeSpan.FromMinutes(2) };
            using var response = await downloader.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 50 * 1024 * 1024) return "artifact too large";
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var destination = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, ct)) > 0)
                {
                    total += count;
                    if (total > 50 * 1024 * 1024) return "artifact too large";
                    await destination.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            string actual;
            await using (var hashInput = File.OpenRead(file))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, ct));
            if (!actual.Equals(sha, StringComparison.OrdinalIgnoreCase)) return "artifact checksum mismatch";
            using var process = new Process();
            process.StartInfo = new(pipx) { UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, RedirectStandardInput = true, CreateNoWindow = true };
            process.StartInfo.ArgumentList.Add("install");
            process.StartInfo.ArgumentList.Add(file);
            process.StartInfo.ArgumentList.Add("--pip-args=--no-deps");
            process.Start();
            process.StandardInput.Close();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); return "installer timeout"; }
            _ = await stdout; _ = await stderr;
            if (process.ExitCode != 0) return "installer failed";
            return await RequirementAvailableAsync(requirement, ct) ? "verified" : "verification failed";
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory);
        }
    }

    private static async Task<bool> RequirementAvailableAsync(string requirement, CancellationToken ct)
    {
        var name = Requirement.Match(requirement).Groups[1].Value;
        var report = await RuntimeInventory.InspectForReportAsync(ct);
        if (!report.Commands.TryGetValue(name, out var version) || version is null) return false;
        var minimum = requirement.Split(">=", 2);
        if (minimum.Length == 1) return true;
        var found = Regex.Match(version, "[0-9]+(?:[.][0-9]+){0,3}").Value;
        return Version.TryParse(Normalize(found), out var current) &&
            Version.TryParse(Normalize(minimum[1]), out var required) && current.CompareTo(required) >= 0;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var parts = value.Split('.').ToList();
        while (parts.Count < 4) parts.Add("0");
        return string.Join('.', parts);
    }
}
