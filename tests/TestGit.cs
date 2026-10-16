using System.Diagnostics;
using Brain.Application;
using Brain.Domain;

namespace Brain.Tests;

internal static class TestGit
{
    public static async Task<string> RunAsync(string root, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + Path.Combine(root, ".git", "no-hooks"),
            "-c", "user.name=Brain Test", "-c", "user.email=brain-test@localhost" }) info.ArgumentList.Add(arg);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("Test git failed: " + await error);
        return await output;
    }
    public static async Task InitializeAsync(string root)
    {
        await RunAsync(root, "init", "--initial-branch=main");
        await RunAsync(root, "config", "core.autocrlf", "false");
        await CommitAsync(root);
    }
    public static async Task CommitAsync(string root)
    {
        await RunAsync(root, "add", "--all");
        await RunAsync(root, "commit", "--allow-empty", "-m", "Test baseline");
    }
    public static void SetPrepared(KnowledgeWriteProposal p, PreparedKnowledgeWrite prepared)
    {
        p.Status = KnowledgeProposalStatus.Validating; p.GitBranch = prepared.Branch; p.ParentCommit = prepared.ParentCommit;
        p.GitCommit = prepared.Commit; p.GitBlob = prepared.Blob; p.BaseContentHash = prepared.BaseContentHash;
        p.ResultContentHash = prepared.ResultContentHash; p.ResultContent = prepared.ResultContent; p.ResultHasUtf8Bom = prepared.HasUtf8Bom;
    }
    public static void ClearReadOnly(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            File.SetAttributes(file, FileAttributes.Normal);
    }
}
