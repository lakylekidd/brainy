using Brain.Domain;
using Brain.Infrastructure;
using Brain.Tests;

namespace Brain.UnitTests;

public sealed class RegistrySourceTests
{
    private const string Skill = """
        id: deploy-service
        name: Deploy Service
        description: >
          Deploy an application
          to production.
        tags: [deployment, production]
        required_tools: [coolify]
        required_permissions: [deployment.production]
        """;

    [Fact]
    public void Parses_folded_text_and_explicit_empty_lists_without_executing_instructions()
    {
        var parsed = FileSystemRegistrySource.Parse(RegistryKind.Skill, "deploy-service", Skill, "hash", "# Instructions");
        Assert.Equal("Deploy an application to production.", parsed.Description);
        Assert.Equal(["coolify"], parsed.RequiredTools);
        Assert.Equal(["deployment.production"], parsed.RequiredPermissions);
        Assert.Equal("skills/deploy-service/manifest.yaml", parsed.Source);
        Assert.Equal("# Instructions", parsed.Instructions);
        var empty = FileSystemRegistrySource.Parse(RegistryKind.Skill, "deploy-service",
            Skill.Replace("[coolify]", "[]").Replace("[deployment.production]", "[]"), "hash");
        Assert.Empty(empty.RequiredTools);
        Assert.Empty(empty.RequiredPermissions);
    }

    [Theory]
    [InlineData("id: deploy-service", "id: ../escape")]
    [InlineData("id: deploy-service", "id: different-id")]
    [InlineData("id: deploy-service", "id: &id deploy-service")]
    [InlineData("name: Deploy Service", "name: !!str Deploy Service")]
    [InlineData("name: Deploy Service", "name: [Deploy, Service]")]
    [InlineData("name: Deploy Service", "name: ''")]
    [InlineData("tags: [deployment, production]", "tags: [deployment, deployment]")]
    [InlineData("required_tools: [coolify]", "required_tools: coolify")]
    [InlineData("required_tools: [coolify]", "required_tools: [../outside]")]
    [InlineData("required_tools: [coolify]", "required_tools: [CON]")]
    [InlineData("required_permissions: [deployment.production]", "required_permissions: ['*']")]
    [InlineData("required_permissions: [deployment.production]", "required_permissions: [deployment.Production]")]
    [InlineData("required_permissions: [deployment.production]", "unknown: [deployment.production]")]
    [InlineData("required_permissions: [deployment.production]", "required_permissions: [deployment.production]\nname: Duplicate")]
    [InlineData("required_permissions: [deployment.production]", "required_permissions: [deployment.production]\n---\nid: second")]
    [InlineData("required_tools: [coolify]", "required_tools: [*alias]")]
    public void Rejects_ambiguous_unsafe_or_malformed_manifests(string from, string to)
    {
        var exception = Assert.Throws<BrainException>(() => FileSystemRegistrySource.Parse(RegistryKind.Skill,
            "deploy-service", Skill.Replace(from, to), "hash"));
        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public void Tool_schema_rejects_unknown_provider_and_missing_permission_list()
    {
        const string tool = "id: coolify\nname: Coolify\ndescription: Deploy applications\nprovider: mcp\npermissions: []\ncapabilities: [deploy_service]";
        Assert.Equal("mcp", FileSystemRegistrySource.Parse(RegistryKind.Tool, "coolify", tool, "hash").Provider);
        Assert.Throws<BrainException>(() => FileSystemRegistrySource.Parse(RegistryKind.Tool, "coolify", tool.Replace("mcp", "shell"), "hash"));
        Assert.Throws<BrainException>(() => FileSystemRegistrySource.Parse(RegistryKind.Tool, "coolify", tool.Replace("permissions: []\n", ""), "hash"));
    }

    [Fact]
    public async Task Reads_only_canonical_layout_and_hashes_skill_instructions_as_well_as_manifest()
    {
        using var files = new VaultSandbox();
        files.Write("skills/deploy-service/manifest.yaml", Skill);
        var instructionPath = files.Write("skills/deploy-service/SKILL.md", "# Deploy");
        Directory.CreateDirectory(Path.Combine(files.Root, "tools"));
        var source = new FileSystemRegistrySource(new() { RootPath = files.Root });
        var before = Assert.Single(await source.ReadAsync(default));
        File.WriteAllText(instructionPath, "# Updated instructions");
        var after = Assert.Single(await source.ReadAsync(default));
        Assert.NotEqual(before.ContentHash, after.ContentHash);
        Assert.Equal("# Updated instructions", after.Instructions);
    }

    [Fact]
    public async Task Rejects_directory_links_and_oversized_or_invalid_utf8_files()
    {
        using var files = new VaultSandbox();
        using var outside = new VaultSandbox();
        Directory.CreateDirectory(Path.Combine(files.Root, "tools"));
        Directory.CreateDirectory(Path.Combine(files.Root, "skills"));
        outside.Write("manifest.yaml", Skill);
        outside.Write("SKILL.md", "# External instructions");
        files.LinkDirectory("skills/deploy-service", outside.Root);
        var source = new FileSystemRegistrySource(new() { RootPath = files.Root });
        Assert.Equal(422, (await Assert.ThrowsAsync<BrainException>(() => source.ReadAsync(default))).StatusCode);
        // Dispose removes the link without traversing its target.
        using var invalid = new VaultSandbox();
        invalid.Write("skills/deploy-service/manifest.yaml", Skill);
        var path = invalid.Write("skills/deploy-service/SKILL.md", new string('x', 65537));
        Directory.CreateDirectory(Path.Combine(invalid.Root, "tools"));
        var invalidSource = new FileSystemRegistrySource(new() { RootPath = invalid.Root });
        Assert.Equal(422, (await Assert.ThrowsAsync<BrainException>(() => invalidSource.ReadAsync(default))).StatusCode);
        File.WriteAllBytes(path, [0xC3, 0x28]);
        Assert.Equal(503, (await Assert.ThrowsAsync<BrainException>(() => invalidSource.ReadAsync(default))).StatusCode);
        Assert.Equal("# External instructions", File.ReadAllText(Path.Combine(outside.Root, "SKILL.md")));
    }
}
