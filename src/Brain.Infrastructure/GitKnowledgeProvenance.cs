using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Brain.Application;

namespace Brain.Infrastructure;

public sealed class GitKnowledgeProvenance(KnowledgeOptions options) : IKnowledgeProvenance
{
    public async Task<DocumentProvenance> ReadAsync(string path, CancellationToken ct)
    {
        var paths = new FileSystemKnowledgeVault(options);
        _ = paths.ResolveForWrite(path); // Reuse the canonical path/link policy before constructing an argument.
        var root = paths.Root();
        var gitDirectory = Path.Combine(root, ".git");
        if (!Directory.Exists(gitDirectory) || (File.GetAttributes(gitDirectory) & FileAttributes.ReparsePoint) != 0) return new();
        var revision = await RunAsync(root, path, false, ct);
        if (revision is null || !Regex.IsMatch(revision, @"\A[a-f0-9]{40,64}\z")) return new();
        var created = await RunAsync(root, path, true, ct);
        return new(revision, DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null);
    }
    private static async Task<string?> RunAsync(string root, string path, bool creation, CancellationToken ct)
    {
        var info = new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        foreach (var arg in new[] { "--literal-pathspecs", "-c", "core.fsmonitor=false", "-c", "log.showSignature=false", "-c", "core.hooksPath=.git/HEAD", "-C", root,
            "log", "-1", creation ? "--format=%aI" : "--format=%H" }) info.ArgumentList.Add(arg);
        if (creation) info.ArgumentList.Add("--diff-filter=A");
        info.ArgumentList.Add("--"); info.ArgumentList.Add(path);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var process = Process.Start(info)!;
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var error = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                var text = await output; _ = await error;
                return process.ExitCode == 0 && text.Length <= 512 ? text.Trim() : null;
            }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); if (ct.IsCancellationRequested) throw; return null; }
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}
