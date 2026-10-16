using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Brain.Application;
using Brain.Domain;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure;

/// <summary>Local, dedicated-checkout writer. No shell, hooks, network, push, reset or checkout commands.</summary>
public sealed class GitKnowledgeWriter(KnowledgeOptions options, IKnowledgeVault vault, ILogger<GitKnowledgeWriter>? logger = null,
    IMetadataDocumentProcessor? metadata = null) : ICanonicalKnowledgeWriter
{
    private readonly FileSystemKnowledgeVault paths = new(options);

    public async Task<PreparedKnowledgeWrite> PrepareAsync(KnowledgeWriteProposal proposal, CancellationToken ct)
    {
        var root = await RepositoryAsync(ct);
        _ = paths.ResolveForWrite(proposal.Path);
        RejectNestedRepository(root, proposal.Path);
        var before = await ReadCurrentAsync(proposal.Path, ct);
        var result = KnowledgeChangePlanner.Plan(proposal.Operation, proposal.Content, proposal.Section, before, proposal.RevisionToken, metadata);
        var data = Encode(result, before?.HasUtf8Bom == true);
        if (data.Length > options.MaximumDocumentBytes) throw new BrainException(413, "Resulting document exceeds the configured size limit.");
        if (before?.ContentHash == Hash(data)) throw new BrainException(400, "The proposal does not change the document.");
        var branch = await BranchAsync(ct);
        var parent = await OidAsync(["rev-parse", "--verify", "HEAD"], ct);
        await CheckAttributesAsync(proposal.Path, ct);
        await EnsureCleanAsync(ct);
        var original = await TreeBlobAsync(parent, proposal.Path, ct);
        if (before is not null && original is null) throw new BrainException(503, "Existing documents must be tracked in the dedicated vault repository.");
        if (original is not null && original.Value.Mode != "100644") throw new BrainException(400, "The writer supports regular non-executable Markdown files only.");
        if (before is not null && Oid((await GitAsync(["hash-object", "--stdin", "--no-filters"], Encode(before.Content, before.HasUtf8Bom), ct: ct)).Output) != original!.Value.Blob)
            throw new BrainException(503, "Canonical target bytes differ from Git history; commit or resolve the existing change first.");
        if (before is null && original is not null) throw new BrainException(409, "Git already tracks the requested new document; restore or reconcile it before proposing creation.");
        if (proposal.Operation == KnowledgeOperation.CreateDocument && (await GitAsync(["check-ignore", "--no-index", "--", proposal.Path], ct: ct, allowFailure: true)).ExitCode == 0)
            throw new BrainException(400, "New knowledge documents must not be ignored by Git.");
        var blob = Oid((await GitAsync(["hash-object", "-w", "--stdin", "--no-filters"], data, ct: ct)).Output);
        var alternateIndex = Path.Combine(root, ".git", "brain-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            await GitAsync(["read-tree", parent], alternateIndex: alternateIndex, ct: ct);
            await GitAsync(["update-index", "--add", "--cacheinfo", "100644", blob, proposal.Path], alternateIndex: alternateIndex, ct: ct);
            var tree = Oid((await GitAsync(["write-tree"], alternateIndex: alternateIndex, ct: ct)).Output);
            var message = $"docs(brain): apply knowledge proposal {proposal.Id}\n\nAgent: {proposal.AgentId}\nProposal: {proposal.Id}\n\nReason:\n{proposal.Reason}\n";
            var commit = Oid((await GitAsync(["commit-tree", tree, "-p", parent], Encoding.UTF8.GetBytes(message), ct: ct)).Output);
            // Keep the prepared object reachable across long outages and Git garbage collection.
            // This ref is not the checked-out branch and does not publish canonical file changes.
            var intentRef = "refs/brain/proposals/" + proposal.Id.ToString("N");
            var previousIntent = await GitAsync(["rev-parse", "--verify", intentRef], ct: ct, allowFailure: true);
            await GitAsync(["update-ref", intentRef, commit,
                previousIntent.ExitCode == 0 ? Oid(previousIntent.Output) : new string('0', commit.Length)], ct: ct);
            return new(branch, parent, commit, blob, before?.ContentHash, Hash(data), result, before?.HasUtf8Bom == true);
        }
        finally { if (File.Exists(alternateIndex)) File.Delete(alternateIndex); }
    }

    public async Task ApplyAsync(KnowledgeWriteProposal proposal, CancellationToken ct)
    {
        if (proposal.Status != KnowledgeProposalStatus.Validating || proposal.GitBranch is null || proposal.ParentCommit is null ||
            proposal.GitCommit is null || proposal.GitBlob is null || proposal.ResultContent is null || proposal.ResultContentHash is null)
            throw new InvalidOperationException("A durable prepared proposal is required before applying knowledge.");
        var root = await RepositoryAsync(ct);
        var fullPath = paths.ResolveForWrite(proposal.Path);
        RejectNestedRepository(root, proposal.Path);
        if (await BranchAsync(ct) != proposal.GitBranch) throw new BrainException(409, "The checked-out vault branch changed.");
        var head = await OidAsync(["rev-parse", "--verify", "HEAD"], ct);
        if (head != proposal.ParentCommit && head != proposal.GitCommit)
            throw new BrainException(409, "Git history changed after the proposal was prepared; manual review is required.");
        var data = Encode(proposal.ResultContent, proposal.ResultHasUtf8Bom);
        if (Hash(data) != proposal.ResultContentHash) throw new InvalidOperationException("Prepared content hash is inconsistent.");
        var expectedTreeBlob = await TreeBlobAsync(proposal.GitCommit, proposal.Path, ct);
        if (expectedTreeBlob?.Blob != proposal.GitBlob) throw new InvalidOperationException("Prepared Git tree is inconsistent.");
        await CheckAttributesAsync(proposal.Path, ct);
        var current = await ReadCurrentAsync(proposal.Path, ct);
        var currentHash = current?.ContentHash;
        if (currentHash != proposal.BaseContentHash && currentHash != proposal.ResultContentHash)
            throw new BrainException(409, "Canonical content changed after approval; it was not overwritten.");
        var parentBlob = await TreeBlobAsync(proposal.ParentCommit, proposal.Path, ct);
        var indexBlob = await IndexBlobAsync(proposal.Path, ct);
        if (indexBlob != parentBlob?.Blob && indexBlob != proposal.GitBlob)
            throw new BrainException(409, "The target's staged Git content changed; it was not overwritten.");
        if (head == proposal.ParentCommit)
        {
            if (currentHash != proposal.BaseContentHash) throw new BrainException(409, "Canonical content changed before Git publication.");
            await EnsureCleanAsync(ct);
            // Publish durable Git history with an expected old value before materializing the file.
            var update = await GitAsync(["update-ref", "-m", $"brain proposal {proposal.Id}", proposal.GitBranch,
                proposal.GitCommit, proposal.ParentCommit], ct: ct, allowFailure: true);
            if (update.ExitCode != 0)
            {
                var actual = await OidAsync(["rev-parse", "--verify", "HEAD"], ct);
                if (actual == proposal.ParentCommit) throw new BrainException(503, "Git publication failed; restore repository write access or resolve Git metadata locks before retrying.");
                if (actual != proposal.GitCommit) throw new BrainException(409, "Git history changed during publication; the file was not modified.");
            }
        }
        // Recovery accepts only the expected old bytes or the exact prepared bytes. Never append twice.
        if (currentHash != proposal.ResultContentHash)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            _ = paths.ResolveForWrite(proposal.Path);
            var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, ".brain-write-" + proposal.Id.ToString("N"));
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary); // Recover only this proposal's interrupted temporary write.
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
                {
                    await stream.WriteAsync(data, ct);
                    await stream.FlushAsync(ct); stream.Flush(flushToDisk: true);
                }
                _ = paths.ResolveForWrite(proposal.Path);
                if ((await ReadCurrentAsync(proposal.Path, ct))?.ContentHash != proposal.BaseContentHash)
                    throw new BrainException(409, "Canonical content changed during publication; it was not overwritten.");
                File.Move(temporary, fullPath, overwrite: proposal.BaseContentHash is not null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        // Update only this path in the real index; unrelated staged entries are never reset.
        if (await BranchAsync(ct) != proposal.GitBranch || await OidAsync(["rev-parse", "--verify", "HEAD"], ct) != proposal.GitCommit)
            throw new BrainException(409, "Git history changed during materialization; manual review is required.");
        var latestIndex = await IndexBlobAsync(proposal.Path, ct);
        if (latestIndex != parentBlob?.Blob && latestIndex != proposal.GitBlob)
            throw new BrainException(409, "Staged target content changed during publication; manual review is required.");
        await GitAsync(["update-index", "--add", "--cacheinfo", "100644", proposal.GitBlob, proposal.Path], ct: ct);
        if ((await ReadCurrentAsync(proposal.Path, ct))?.ContentHash != proposal.ResultContentHash)
            throw new BrainException(409, "Canonical content changed after publication; manual review is required.");
    }

    private async Task<string> RepositoryAsync(CancellationToken ct)
    {
        var root = paths.Root();
        var gitDirectory = Path.Combine(root, ".git");
        if (!Directory.Exists(gitDirectory) || (File.GetAttributes(gitDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new BrainException(503, "Writer requires a dedicated initialized Git checkout rooted at the vault, with a physical .git directory.");
        var top = (await GitAsync(["rev-parse", "--show-toplevel"], ct: ct)).Output.TrimEnd('\r', '\n');
        if (!Path.GetFullPath(top).Equals(Path.TrimEndingDirectorySeparator(root), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new BrainException(503, "The configured vault must be the Git repository root.");
        return root;
    }
    private static void RejectNestedRepository(string root, string path)
    {
        var parent = Path.GetDirectoryName(Path.Combine(root, path))!;
        while (!Path.GetFullPath(parent).Equals(Path.GetFullPath(root), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (Path.Exists(Path.Combine(parent, ".git"))) throw new BrainException(400, "Nested knowledge repositories are not writable.");
            parent = Path.GetDirectoryName(parent)!;
        }
    }
    private async Task<VaultDocument?> ReadCurrentAsync(string path, CancellationToken ct)
    {
        try { return await vault.ReadAsync(path, ct); }
        catch (BrainException e) when (e.StatusCode == 404) { return null; }
    }
    private async Task EnsureCleanAsync(CancellationToken ct)
    {
        if ((await GitAsync(["status", "--porcelain=v1", "-z", "--untracked-files=all"], ct: ct)).Output.Length != 0)
            throw new BrainException(503, "Vault Git checkout has uncommitted changes; commit or resolve them before the writer retries.");
    }
    private async Task CheckAttributesAsync(string path, CancellationToken ct)
    {
        var fields = (await GitAsync(["check-attr", "-z", "filter", "working-tree-encoding", "text", "eol", "--", path], ct: ct)).Output.Split('\0');
        for (var i = 2; i < fields.Length; i += 3)
            if (fields[i] is not "unspecified" and not "unset")
                throw new BrainException(503, "Writer requires raw UTF-8 Git paths without filters, encoding or text/eol attributes (use *.md -text).");
    }
    private async Task<string> BranchAsync(CancellationToken ct)
    {
        var result = await GitAsync(["symbolic-ref", "--quiet", "HEAD"], ct: ct, allowFailure: true);
        var branch = result.Output.Trim();
        if (result.ExitCode != 0 || !branch.StartsWith("refs/heads/", StringComparison.Ordinal))
            throw new BrainException(503, "Writer requires a checked-out named Git branch.");
        return branch;
    }
    private async Task<(string Mode, string Blob)?> TreeBlobAsync(string commit, string path, CancellationToken ct)
    {
        var result = (await GitAsync(["ls-tree", "-z", commit, "--", path], ct: ct)).Output;
        if (result.Length == 0) return null;
        var fields = result[..result.IndexOf('\t')].Split(' ');
        if (fields.Length != 3 || fields[1] != "blob") throw new BrainException(400, "Knowledge target is not a regular Git file.");
        return (fields[0], fields[2]);
    }
    private async Task<string?> IndexBlobAsync(string path, CancellationToken ct)
    {
        var result = (await GitAsync(["ls-files", "--stage", "-z", "--", path], ct: ct)).Output;
        if (result.Length == 0) return null;
        var fields = result[..result.IndexOf('\t')].Split(' ');
        if (fields.Length != 3 || fields[2] != "0" || result.Count(c => c == '\0') != 1)
            throw new BrainException(409, "The target has unresolved Git index entries.");
        return fields[1];
    }
    private async Task<string> OidAsync(string[] args, CancellationToken ct) => Oid((await GitAsync(args, ct: ct)).Output);
    private static string Oid(string output)
    {
        var oid = output.Trim();
        if (oid.Length is not 40 and not 64 || !oid.All(Uri.IsHexDigit)) throw new BrainException(503, "Git did not return a valid object ID; ensure the vault has an initial commit.");
        return oid;
    }
    public static byte[] Encode(string content, bool bom)
    {
        var data = new UTF8Encoding(false, true).GetBytes(content);
        return bom ? [0xEF, 0xBB, 0xBF, .. data] : data;
    }
    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private sealed record GitResult(int ExitCode, string Output);
    private async Task<GitResult> GitAsync(string[] args, byte[]? input = null, string? alternateIndex = null,
        CancellationToken ct = default, bool allowFailure = false)
    {
        var root = paths.Root();
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        foreach (var arg in new[] { "-c", "core.autocrlf=false", "-c", "core.fsmonitor=false", "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=" + Path.Combine(root, ".git", "HEAD"), "-c", "safe.directory=" + root }) start.ArgumentList.Add(arg);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["GIT_LITERAL_PATHSPECS"] = "1";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GIT_AUTHOR_NAME"] = "Homelab Brain";
        start.Environment["GIT_AUTHOR_EMAIL"] = "brain@localhost";
        start.Environment["GIT_COMMITTER_NAME"] = "Homelab Brain";
        start.Environment["GIT_COMMITTER_EMAIL"] = "brain@localhost";
        if (alternateIndex is not null) start.Environment["GIT_INDEX_FILE"] = alternateIndex;
        using var process = new Process { StartInfo = start };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            if (input is not null) await process.StandardInput.BaseStream.WriteAsync(input, timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var result = new GitResult(process.ExitCode, await output);
            var diagnostic = await error;
            if (result.ExitCode != 0 && !allowFailure)
            {
                logger?.LogWarning("Git {Command} failed with exit code {ExitCode}: {Diagnostic}", args[0], result.ExitCode, diagnostic[..Math.Min(diagnostic.Length, 4000)]);
                throw new BrainException(503, $"Git {args[0]} failed; inspect the dedicated vault repository.");
            }
            return result;
        }
        catch (Win32Exception) { throw new BrainException(503, "Git is unavailable to the Brain Writer."); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            ct.ThrowIfCancellationRequested();
            throw new BrainException(503, "Git operation timed out; the prepared proposal will be retried.");
        }
        catch (IOException) { throw new BrainException(503, "Git I/O failed; the prepared proposal will be retried."); }
    }
}
