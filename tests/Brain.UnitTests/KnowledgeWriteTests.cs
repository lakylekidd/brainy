using System.Text;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Brain.Tests;

namespace Brain.UnitTests;

public sealed class KnowledgeWriteTests
{
    [Fact]
    public void Section_replacement_preserves_frontmatter_heading_siblings_and_ignores_fenced_headings()
    {
        const string content = "---\nid: human\n---\n# Cluster\n## Recovery\nOld body.\n```md\n## Not a heading\n```\n### Child\nOld child.\n## Status\nKeep status.\n";
        var before = Snapshot(content);
        var after = KnowledgeChangePlanner.Plan(KnowledgeOperation.ReplaceSection, "New recovery.", "Cluster / Recovery", before, before.RevisionToken);
        Assert.StartsWith("---\nid: human\n---\n# Cluster\n## Recovery\nNew recovery.\n## Status", after);
        Assert.EndsWith("Keep status.\n", after);
        Assert.DoesNotContain("Old child", after);
    }
    [Fact]
    public void Duplicate_or_missing_sections_and_stale_tokens_fail_instead_of_guessing()
    {
        var before = Snapshot("# Cluster\n## Recovery\nFirst\n## Recovery\nSecond\n");
        Assert.Equal(400, Assert.Throws<BrainException>(() => KnowledgeChangePlanner.Plan(KnowledgeOperation.ReplaceSection, "New", "Cluster / Recovery", before, before.RevisionToken)).StatusCode);
        Assert.Equal(400, Assert.Throws<BrainException>(() => KnowledgeChangePlanner.Plan(KnowledgeOperation.ReplaceSection, "New", "Missing", before, before.RevisionToken)).StatusCode);
        Assert.Equal(409, Assert.Throws<BrainException>(() => KnowledgeChangePlanner.Plan(KnowledgeOperation.Append, "New", null, before, "stale")).StatusCode);
    }
    [Fact]
    public void Append_and_heading_without_final_newline_preserve_document_boundaries()
    {
        var before = Snapshot("# Cluster\r\nExisting.");
        Assert.Equal("# Cluster\r\nExisting.\r\nNew.", KnowledgeChangePlanner.Plan(KnowledgeOperation.Append, "New.", null, before, before.RevisionToken));
        before = Snapshot("# Empty section");
        Assert.Equal("# Empty section\nNew body.\n", KnowledgeChangePlanner.Plan(KnowledgeOperation.ReplaceSection, "New body.", "Empty section", before, before.RevisionToken));
    }
    [Fact]
    public async Task Preparing_is_side_effect_free_for_canonical_files_and_application_is_idempotent()
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original\nExisting.");
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var before = await vault.ReadAsync("doc.md", default);
        var proposal = Proposal(before, KnowledgeOperation.Append, "Observation $(never execute): `text`.");
        var oldHead = await TestGit.RunAsync(sandbox.Root, "rev-parse", "HEAD");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        Assert.Equal(oldHead, await TestGit.RunAsync(sandbox.Root, "rev-parse", "HEAD"));
        Assert.Equal(proposal.GitCommit, (await TestGit.RunAsync(sandbox.Root, "rev-parse", "refs/brain/proposals/" + proposal.Id.ToString("N"))).Trim());
        await TestGit.RunAsync(sandbox.Root, "gc", "--prune=now");
        Assert.Equal(before.Content, File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
        await writer.ApplyAsync(proposal, default);
        await writer.ApplyAsync(proposal, default);
        var content = File.ReadAllText(Path.Combine(sandbox.Root, "doc.md"));
        Assert.Equal(1, content.Split("Observation").Length - 1);
        Assert.Equal("2", (await TestGit.RunAsync(sandbox.Root, "rev-list", "--count", "HEAD")).Trim());
        Assert.Empty(await TestGit.RunAsync(sandbox.Root, "status", "--porcelain"));
        Assert.Contains("Agent: codex", await TestGit.RunAsync(sandbox.Root, "log", "-1", "--format=%B"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Published_commit_recovers_with_old_or_already_materialized_bytes(bool alreadyWritten)
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original\nExisting.");
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "Recovered once.");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        await TestGit.RunAsync(sandbox.Root, "update-ref", proposal.GitBranch!, proposal.GitCommit!, proposal.ParentCommit!);
        if (alreadyWritten) File.WriteAllBytes(Path.Combine(sandbox.Root, "doc.md"), GitKnowledgeWriter.Encode(proposal.ResultContent!, proposal.ResultHasUtf8Bom));
        await writer.ApplyAsync(proposal, default);
        Assert.Equal(proposal.ResultContent, File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
        Assert.Empty(await TestGit.RunAsync(sandbox.Root, "status", "--porcelain"));
        Assert.Equal("2", (await TestGit.RunAsync(sandbox.Root, "rev-list", "--count", "HEAD")).Trim());
    }
    [Fact]
    public async Task Newer_content_after_publication_is_never_overwritten_by_recovery()
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original\nExisting.");
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "Proposal.");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        await TestGit.RunAsync(sandbox.Root, "update-ref", proposal.GitBranch!, proposal.GitCommit!, proposal.ParentCommit!);
        File.WriteAllText(Path.Combine(sandbox.Root, "doc.md"), "Newer human knowledge.");
        Assert.Equal(409, (await Assert.ThrowsAsync<BrainException>(() => writer.ApplyAsync(proposal, default))).StatusCode);
        Assert.Equal("Newer human knowledge.", File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
    }
    [Fact]
    public async Task Bom_and_crlf_are_preserved_in_both_canonical_bytes_and_git_history()
    {
        using var sandbox = new VaultSandbox();
        var file = sandbox.Write("doc.md", "unused");
        File.WriteAllText(file, "# Original\r\nExisting.\r\n", new UTF8Encoding(true));
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "New.\r\n");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        await writer.ApplyAsync(proposal, default);
        Assert.True(File.ReadAllBytes(file).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal("# Original\r\nExisting.\r\nNew.\r\n", (await vault.ReadAsync("doc.md", default)).Content);
        Assert.Empty(await TestGit.RunAsync(sandbox.Root, "status", "--porcelain"));
    }
    [Theory]
    [InlineData("-notes.md")]
    [InlineData("Services/[prod],deploy.md")]
    public async Task Create_paths_are_literal_and_new_directories_are_only_created_during_apply(string path)
    {
        using var sandbox = new VaultSandbox();
        await TestGit.InitializeAsync(sandbox.Root);
        var (_, writer) = Services(sandbox.Root);
        var proposal = new KnowledgeWriteProposal { AgentId = "codex", Path = path, Operation = KnowledgeOperation.CreateDocument, Content = "# New\nContent", Reason = "Observed" };
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        Assert.False(File.Exists(Path.Combine(sandbox.Root, path)));
        await writer.ApplyAsync(proposal, default);
        Assert.Equal(proposal.Content, File.ReadAllText(Path.Combine(sandbox.Root, path)));
        Assert.Empty(await TestGit.RunAsync(sandbox.Root, "status", "--porcelain"));
    }
    [Fact]
    public async Task Dirty_staged_changes_and_git_history_changes_are_preserved()
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original");
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "New");
        sandbox.Write("other.md", "Human staged change");
        await TestGit.RunAsync(sandbox.Root, "add", "--", "other.md");
        var staged = await TestGit.RunAsync(sandbox.Root, "diff", "--cached");
        Assert.Equal(503, (await Assert.ThrowsAsync<BrainException>(() => writer.PrepareAsync(proposal, default))).StatusCode);
        Assert.Equal(staged, await TestGit.RunAsync(sandbox.Root, "diff", "--cached"));
        await TestGit.CommitAsync(sandbox.Root);
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        sandbox.Write("other.md", "Later human commit");
        await TestGit.CommitAsync(sandbox.Root);
        Assert.Equal(409, (await Assert.ThrowsAsync<BrainException>(() => writer.ApplyAsync(proposal, default))).StatusCode);
        Assert.Equal("# Original", File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
    }
    [Fact]
    public async Task Linked_creation_targets_are_rejected_even_with_a_valid_dedicated_git_checkout()
    {
        using var outside = new VaultSandbox();
        using var sandbox = new VaultSandbox();
        await TestGit.InitializeAsync(sandbox.Root);
        sandbox.LinkDirectory("linked", outside.Root);
        var (_, writer) = Services(sandbox.Root);
        var proposal = new KnowledgeWriteProposal { AgentId = "codex", Path = "linked/new.md", Operation = KnowledgeOperation.CreateDocument, Content = "# New", Reason = "Test" };
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => writer.PrepareAsync(proposal, default))).StatusCode);
        Assert.False(File.Exists(Path.Combine(outside.Root, "new.md")));
    }

    [Fact]
    public async Task Repository_hooks_are_not_executed_by_preparation_or_publication()
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original");
        await TestGit.InitializeAsync(sandbox.Root);
        foreach (var name in new[] { "post-index-change", "reference-transaction" })
        {
            var hook = Path.Combine(sandbox.Root, ".git", "hooks", name);
            File.WriteAllText(hook, "#!/bin/sh\nprintf ran > .git/hook-ran\n");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "Safe text");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        await writer.ApplyAsync(proposal, default);
        Assert.False(File.Exists(Path.Combine(sandbox.Root, ".git", "hook-ran")));
    }

    [Fact]
    public async Task Git_metadata_lock_failure_is_retryable_without_modifying_canonical_content()
    {
        using var sandbox = new VaultSandbox();
        sandbox.Write("doc.md", "# Original");
        await TestGit.InitializeAsync(sandbox.Root);
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "Retry safely");
        TestGit.SetPrepared(proposal, await writer.PrepareAsync(proposal, default));
        var referenceLock = Path.Combine(sandbox.Root, ".git", "refs", "heads", "main.lock");
        File.WriteAllText(referenceLock, "test-held-lock");
        Assert.Equal(503, (await Assert.ThrowsAsync<BrainException>(() => writer.ApplyAsync(proposal, default))).StatusCode);
        Assert.Equal("# Original", File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
        File.Delete(referenceLock);
        await writer.ApplyAsync(proposal, default);
        Assert.Contains("Retry safely", File.ReadAllText(Path.Combine(sandbox.Root, "doc.md")));
    }

    [Fact]
    public async Task Hidden_worktree_edits_are_not_silently_committed_with_a_proposal()
    {
        using var sandbox = new VaultSandbox();
        var file = sandbox.Write("doc.md", "# Original");
        await TestGit.InitializeAsync(sandbox.Root);
        await TestGit.RunAsync(sandbox.Root, "update-index", "--assume-unchanged", "--", "doc.md");
        File.WriteAllText(file, "# Uncommitted hidden edit");
        Assert.Empty(await TestGit.RunAsync(sandbox.Root, "status", "--porcelain"));
        var (vault, writer) = Services(sandbox.Root);
        var proposal = Proposal(await vault.ReadAsync("doc.md", default), KnowledgeOperation.Append, "Observation");
        Assert.Equal(503, (await Assert.ThrowsAsync<BrainException>(() => writer.PrepareAsync(proposal, default))).StatusCode);
        Assert.Equal("# Uncommitted hidden edit", File.ReadAllText(file));
    }

    private static (FileSystemKnowledgeVault Vault, GitKnowledgeWriter Writer) Services(string root)
    {
        var options = new KnowledgeOptions { VaultPath = root + Path.DirectorySeparatorChar };
        var vault = new FileSystemKnowledgeVault(options);
        return (vault, new(options, vault));
    }
    private static KnowledgeWriteProposal Proposal(VaultDocument before, KnowledgeOperation operation, string content) => new()
    { AgentId = "codex", Path = before.Path, RevisionToken = before.RevisionToken, Operation = operation, Content = content, Reason = "Observed during testing" };
    private static VaultDocument Snapshot(string content) => new("doc.md", content, "hash", "token", DateTimeOffset.UtcNow);
}
