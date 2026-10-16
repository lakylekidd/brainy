using System.Text;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Brain.Tests;

namespace Brain.UnitTests;

public sealed class KnowledgeTests
{
    [Fact]
    public void Chunking_preserves_sections_lines_and_code_fences_while_excluding_frontmatter()
    {
        const string source = "---\ntitle: Human title\n---\n# Cluster\nOverview.\n## Recovery\n```sh\n# not a document heading\nrestart corosync\n```\n\nDone.";
        var parsed = new MarkdownChunker().Chunk("Infrastructure/cluster.md", source);
        Assert.Equal("Cluster", parsed.Title);
        Assert.Equal(2, parsed.Chunks.Count);
        Assert.Equal("Cluster / Recovery", parsed.Chunks[1].Heading);
        Assert.Equal(6, parsed.Chunks[1].StartLine);
        Assert.Equal(12, parsed.Chunks[1].EndLine);
        Assert.Contains("# not a document heading", parsed.Chunks[1].Content);
        Assert.DoesNotContain(parsed.Chunks, c => c.Content.Contains("title: Human title"));
    }

    [Fact]
    public void Large_lines_are_split_without_losing_content_or_breaking_surrogates()
    {
        var content = new string('a', 1799) + "😀" + new string('b', 2000);
        var parsed = new MarkdownChunker().Chunk("large-document.md", content);
        Assert.Equal("large document", parsed.Title);
        Assert.Equal(content, string.Concat(parsed.Chunks.Select(c => c.Content)));
        Assert.All(parsed.Chunks, c => Assert.InRange(c.Content.Length, 1, MarkdownChunker.MaximumChunkCharacters));
        Assert.All(parsed.Chunks, c => new UTF8Encoding(false, true).GetBytes(c.Content));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("Infrastructure/../../outside.md")]
    [InlineData("/etc/passwd.md")]
    [InlineData("C:/outside.md")]
    [InlineData("Infrastructure\\outside.md")]
    [InlineData(".git/config.md")]
    [InlineData("Infrastructure//cluster.md")]
    [InlineData("Infrastructure/cluster.md/..")]
    [InlineData("passwords.txt")]
    [InlineData("service.md:stream")]
    [InlineData("Folder /document.md")]
    [InlineData("NUL.md")]
    [InlineData("COM1/page.md")]
    [InlineData("LPT¹.md")]
    public async Task Unsafe_or_non_markdown_paths_are_rejected(string path)
    {
        using var sandbox = new VaultSandbox();
        var vault = Vault(sandbox);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => vault.ReadAsync(path, default))).StatusCode);
    }

    [Fact]
    public async Task Tokens_are_stable_bound_to_path_and_content_and_handle_utf8_bom()
    {
        using var sandbox = new VaultSandbox();
        var file = sandbox.Write("Infrastructure/cluster.md", "# Cluster\r\nInitial 😀");
        var vault = Vault(sandbox);
        var first = await vault.ReadAsync("Infrastructure/cluster.md", default);
        Assert.Equal(first, await vault.ReadAsync(first.Path, default));
        Assert.True(RevisionTokens.Matches(first.RevisionToken, first.Path, first.ContentHash));
        Assert.False(RevisionTokens.Matches(first.RevisionToken, "Other/cluster.md", first.ContentHash));
        File.WriteAllText(file, "# Cluster\r\nChanged 😀", new UTF8Encoding(true));
        var updated = await vault.ReadAsync(first.Path, default);
        Assert.StartsWith("# Cluster", updated.Content);
        Assert.NotEqual(first.RevisionToken, updated.RevisionToken);
        Assert.False(RevisionTokens.Matches(first.RevisionToken, updated.Path, updated.ContentHash));
    }

    [Fact]
    public async Task Scanner_and_reader_exclude_linked_directories_and_dot_directories()
    {
        using var outside = new VaultSandbox();
        outside.Write("private.md", "Do not index this");
        using var vaultDirectory = new VaultSandbox();
        vaultDirectory.Write(".git/internal.md", "Do not index this either");
        vaultDirectory.Write("Normal/page.md", "# Normal page");
        vaultDirectory.LinkDirectory("linked", outside.Root);
        vaultDirectory.LinkDirectory("cycle", vaultDirectory.Root);
        var vault = Vault(vaultDirectory);
        Assert.Equal(["Normal/page.md"], vault.ListMarkdownPaths(default));
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => vault.ReadAsync("linked/private.md", default))).StatusCode);
        var linkedRoot = new FileSystemKnowledgeVault(new() { VaultPath = Path.Combine(vaultDirectory.Root, "linked") });
        Assert.Throws<BrainException>(() => linkedRoot.ListMarkdownPaths(default));
    }

    [Fact]
    public async Task Invalid_encoding_oversized_and_missing_documents_fail_explicitly()
    {
        using var sandbox = new VaultSandbox();
        var file = sandbox.Write("invalid.md", "unused");
        File.WriteAllBytes(file, [0xC3, 0x28]);
        var vault = Vault(sandbox);
        Assert.Equal(422, (await Assert.ThrowsAsync<BrainException>(() => vault.ReadAsync("invalid.md", default))).StatusCode);
        var limited = new FileSystemKnowledgeVault(new() { VaultPath = sandbox.Root, MaximumDocumentBytes = 1 });
        Assert.Equal(413, (await Assert.ThrowsAsync<BrainException>(() => limited.ReadAsync("invalid.md", default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<BrainException>(() => vault.ReadAsync("missing.md", default))).StatusCode);
        var unavailable = new FileSystemKnowledgeVault(new() { VaultPath = Path.Combine(sandbox.Root, "missing") });
        Assert.Equal(503, Assert.Throws<BrainException>(() => unavailable.ListMarkdownPaths(default)).StatusCode);
    }

    private static FileSystemKnowledgeVault Vault(VaultSandbox sandbox) => new(new() { VaultPath = sandbox.Root });
}
