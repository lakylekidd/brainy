using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Brain.Client;

public sealed record RuntimeSnapshot(string Environment, string OperatingSystem, string Architecture,
    IReadOnlyDictionary<string, string?> Commands);

public static class RuntimeInventory
{
    private static readonly string[] Commands = ["dotnet", "python3", "python", "node", "docker", "git", "claude", "codex", "hermes"];

    public static RuntimeSnapshot Inspect() => new("host", RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        Commands.ToDictionary(command => command, FindExecutable));

    public static async Task<RuntimeReport> InspectForReportAsync(CancellationToken ct)
    {
        var versions = new Dictionary<string, string?>();
        foreach (var name in Commands)
            if (FindExecutable(name) is { } executable)
                versions[name] = await VersionAsync(executable, ct) ?? "installed";
        return new("host", "host", RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            await DockerAvailableAsync(ct), versions, true);
    }

    private static async Task<string?> VersionAsync(string executable, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var process = new Process();
            process.StartInfo = new(executable, "--version") { UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            process.Start();
            var output = await process.StandardOutput.ReadLineAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            output ??= await process.StandardError.ReadLineAsync(timeout.Token);
            return output is { Length: > 0 } ? output[..Math.Min(output.Length, 200)] : null;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { return null; }
    }

    private static async Task<bool> DockerAvailableAsync(CancellationToken ct)
    {
        if (FindExecutable("docker") is not { } docker) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var process = new Process();
            process.StartInfo = new(docker) { UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            process.StartInfo.ArgumentList.Add("info");
            process.StartInfo.ArgumentList.Add("--format");
            process.StartInfo.ArgumentList.Add("{{.ServerVersion}}");
            process.Start();
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { return false; }
    }

    public static string? FindExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat" } : new[] { "" };
        foreach (var path in paths.Where(Directory.Exists))
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(path, name + extension);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }
}

public sealed record RuntimeReport(string EnvironmentId, string Kind, string OperatingSystem,
    string Architecture, bool DockerAvailable, IReadOnlyDictionary<string, string?> Commands, bool Healthy);
