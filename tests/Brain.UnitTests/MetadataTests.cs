using System.Net;
using System.Text;
using System.Text.Json;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;

namespace Brain.UnitTests;

public sealed class MetadataTests
{
    [Fact]
    public void Missing_frontmatter_generates_stable_metadata_from_the_filename_and_preserves_body()
    {
        var processor = new MetadataDocumentProcessor(new());
        var source = Snapshot("# Human heading\nBody with --- and code.\n");
        Assert.Contains(processor.Inspect(source).Issues, i => i.Code == "missing_frontmatter");
        var patch = processor.BuildPatch(source, new(), null);
        Assert.Equal("Deploy coolify", patch.Fields["title"].GetString());
        Assert.StartsWith("deploy-coolify-", patch.Fields["id"].GetString());
        Assert.Equal("Procedures/deploy-coolify.md", patch.Fields["path"].GetString());
        Assert.False(patch.Fields.ContainsKey("created_at"));
        Assert.False(patch.Fields.ContainsKey("git_revision"));
        Assert.False(patch.Fields.ContainsKey("type"));
        var after = processor.Apply(source, Encode(patch));
        Assert.EndsWith(source.Content, after);
        Assert.Empty(processor.Inspect(source with { Content = after }).Issues);
        Assert.Equal(patch.Fields["id"].GetString(), processor.BuildPatch(source, new(), null).Fields["id"].GetString());
        Assert.NotEqual(patch.Fields["id"].GetString(), processor.BuildPatch(source with { Path = "Other/deploy-coolify.md" }, new(), null).Fields["id"].GetString());
    }

    [Fact]
    public void Generated_unicode_values_round_trip_through_safe_yaml_rendering()
    {
        var source = Snapshot("# Body\n") with { Path = "Projects/rocket-🚀.md" };
        var processor = new MetadataDocumentProcessor(new());
        var patch = processor.BuildPatch(source, new(), new SemanticMetadata { Summary = "A 🚀 deployment.", Confidence = 0.8 });
        var after = processor.Apply(source, Encode(patch));
        var inspection = processor.Inspect(source with { Content = after });
        Assert.Empty(inspection.Issues);
        Assert.Equal("A 🚀 deployment.", inspection.ValidFields["summary"].GetString());
        Assert.Equal("rocket-🚀.md", inspection.ValidFields["filename"].GetString());
    }

    [Fact]
    public void Valid_minimal_frontmatter_needs_no_optional_fields_or_model()
    {
        var inspection = new MetadataDocumentProcessor(new()).Inspect(Snapshot("---\nid: human-id\ntitle: A human title\nmetadata_version: 1\n---\nBody"));
        Assert.True(inspection.CanEdit);
        Assert.Empty(inspection.Issues);
        Assert.Equal("Body", inspection.Body);
    }

    [Fact]
    public void Preserves_human_values_comments_unknown_nested_fields_styles_and_crlf_body()
    {
        const string before = "---\r\n# Keep this comment\r\nid: human-id\r\ntitle: 'Human 🚀 title' # human title comment\r\ntype: procedure\r\ntags: [HumanTag, 'SQL Server']\r\nsummary: >\r\n  A human summary.\r\ncustom:\r\n  owner: Willem\r\n  values: [1, 2]\r\n---\r\n# Body\r\nKeep exact text.\r\n";
        var source = Snapshot(before);
        var processor = new MetadataDocumentProcessor(new());
        var semantic = new SemanticMetadata { Type = "note", Summary = "Generated", Tags = ["generated"], Systems = ["coolify"], Confidence = 0.99 };
        var patch = processor.BuildPatch(source, new(new string('a', 40), source.ModifiedAt), semantic);
        Assert.DoesNotContain("title", patch.Fields.Keys);
        Assert.DoesNotContain("summary", patch.Fields.Keys);
        Assert.DoesNotContain("type", patch.Fields.Keys);
        Assert.DoesNotContain("tags", patch.Fields.Keys);
        var after = processor.Apply(source, Encode(patch));
        Assert.Contains(before[..before.LastIndexOf("---", StringComparison.Ordinal)], after);
        Assert.EndsWith("# Body\r\nKeep exact text.\r\n", after);
        Assert.Equal("procedure", processor.Inspect(source with { Content = after }).ValidFields["type"].GetString());
    }

    [Theory]
    [InlineData("title: [bad, title]\nmetadata_version: broken")]
    [InlineData("title: {bad: title}\nmetadata_version: broken")]
    [InlineData("title:\n  bad: title\nmetadata_version: broken")]
    [InlineData("title: |\nmetadata_version: 0")]
    [InlineData("title:\nmetadata_version: '1'")]
    public void Invalid_deterministic_values_can_be_repaired_without_losing_adjacent_metadata(string invalid)
    {
        var processor = new MetadataDocumentProcessor(new());
        var source = Snapshot("---\n# 🚀 Keep Unicode\nid: human-id\n" + invalid + "\ncustom: preserve\n---\nBody\n");
        Assert.NotEmpty(processor.Inspect(source).Issues);
        var after = processor.Apply(source, Encode(processor.BuildPatch(source, new(), null)));
        Assert.Contains("custom: preserve", after);
        Assert.Contains("# 🚀 Keep Unicode", after);
        Assert.Contains("id: human-id", after);
        Assert.Empty(processor.Inspect(source with { Content = after }).Issues);
    }

    [Theory]
    [InlineData("---\ntitle: [invalid\n---\nBody")]
    [InlineData("---\nid: one\nid: two\n---\nBody")]
    [InlineData("---\nid: &anchor value\ntitle: *anchor\n---\nBody")]
    [InlineData("---\ntitle: !!str value\n---\nBody")]
    [InlineData("---\ntitle: Unclosed frontmatter")]
    [InlineData("---\n{id: human-id, title: Human}\n---\nBody")]
    public void Ambiguous_yaml_is_reported_and_never_replaced_with_guessed_metadata(string content)
    {
        var processor = new MetadataDocumentProcessor(new());
        var source = Snapshot(content);
        var inspection = processor.Inspect(source);
        Assert.False(inspection.CanEdit);
        Assert.Contains(inspection.Issues, i => i.Code is "invalid_yaml" or "unsupported_yaml");
        Assert.Throws<BrainException>(() => processor.BuildPatch(source, new(), null));
        Assert.Equal(content, source.Content);
    }

    [Fact]
    public void String_fields_and_lists_reject_implicit_yaml_numbers_and_booleans()
    {
        var source = Snapshot("---\nid: human\ntitle: false\nmetadata_version: 1\nsummary: 123\ntype: true\ntags: [1, true]\n---\nBody");
        var inspection = new MetadataDocumentProcessor(new()).Inspect(source);
        foreach (var field in new[] { "title", "summary", "type", "tags" })
        {
            Assert.Contains(inspection.Issues, i => i.Field == field && i.Code == "invalid_field");
            Assert.False(inspection.ValidFields.ContainsKey(field));
        }
    }

    [Fact]
    public void Schema_upgrade_is_proposed_and_future_schema_is_never_downgraded()
    {
        var source = Snapshot("---\nid: human-id\ntitle: Human\nmetadata_generated: true\nmetadata_version: 1\n---\nBody");
        var options = new MetadataOptions { SchemaVersion = 2, DocumentTypes = ["checklist"] };
        var processor = new MetadataDocumentProcessor(options);
        Assert.Contains(processor.Inspect(source).Issues, i => i.Code == "outdated_schema");
        var patch = processor.BuildPatch(source, new(), new SemanticMetadata { Type = "checklist", Confidence = 0.8 });
        Assert.Equal(2, patch.Fields["metadata_version"].GetInt32());
        var after = processor.Apply(source, Encode(patch));
        Assert.Empty(processor.Inspect(source with { Content = after }).Issues);
        Assert.False(new MetadataDocumentProcessor(new()).Inspect(source with { Content = after }).CanEdit);
        options.SchemaVersion = 3;
        Assert.Throws<BrainException>(() => processor.Apply(source, Encode(patch)));
    }

    [Fact]
    public void Body_hash_ignores_frontmatter_so_applying_metadata_does_not_create_an_enrichment_loop()
    {
        var processor = new MetadataDocumentProcessor(new());
        var source = Snapshot("# Original body\n");
        var enriched = source with { Content = processor.Apply(source, Encode(processor.BuildPatch(source, new(), null))) };
        Assert.Empty(processor.Inspect(enriched).Issues);
        var modified = enriched with { Content = enriched.Content.Replace("Original body", "Changed body") };
        Assert.Contains(processor.Inspect(modified).Issues, i => i.Code == "stale_content_hash");
        var patch = processor.BuildPatch(modified, new(), null);
        Assert.Equal("content_hash", Assert.Single(patch.Fields).Key);
        var next = processor.Apply(modified, Encode(patch));
        Assert.Empty(processor.Inspect(modified with { Content = next }).Issues);
    }

    [Theory]
    [InlineData("YAML: type: procedure")]
    [InlineData("{\"confidence\":1,\"permissions\":[\"admin\"]}")]
    [InlineData("{\"confidence\":1,\"type\":\"unknown\"}")]
    [InlineData("{\"confidence\":1.1}")]
    [InlineData("{\"type\":\"procedure\"}")]
    [InlineData("{\"confidence\":1,\"tags\":[1]}")]
    [InlineData("{\"confidence\":1,\"tags\":[\" a\",\"a\"]}")]
    [InlineData("{\"confidence\":1,\"summary\":{\"value\":\"bad\"}}")]
    [InlineData("{\"confidence\":1,\"confidence\":0}")]
    public void Rejects_malformed_unknown_or_invalid_semantic_provider_output(string json) =>
        Assert.Equal(422, Assert.Throws<BrainException>(() => MetadataJson.ParseSemantic(json, new())).StatusCode);

    [Fact]
    public void Metadata_patches_cannot_modify_a_valid_human_field_or_add_unknown_keys()
    {
        var source = Snapshot("---\nid: human-id\ntitle: Human title\n---\nBody");
        var processor = new MetadataDocumentProcessor(new());
        var patch = processor.BuildPatch(source, new(), null);
        patch.Fields["title"] = JsonSerializer.SerializeToElement("Overwrite human title");
        Assert.Throws<BrainException>(() => processor.Apply(source, Encode(patch)));
        patch.Fields.Remove("title"); patch.Fields["permission"] = JsonSerializer.SerializeToElement("admin");
        Assert.Throws<BrainException>(() => processor.Apply(source, Encode(patch)));
    }

    [Fact]
    public async Task Optional_http_provider_requests_json_and_returns_structured_content_for_application_validation()
    {
        var handler = new ProviderHandler();
        using var client = new HttpClient(handler);
        var provider = new CompatibleMetadataProvider(client, new() { Endpoint = "http://localhost:11434/v1/chat/completions", Model = "test-model", ApiKey = "test-key" });
        var json = await provider.GenerateJsonAsync(new("doc.md", "Untrusted document body", new Dictionary<string, JsonElement>(), ["procedure"]), default);
        Assert.Equal("procedure", MetadataJson.ParseSemantic(json, new()).Type);
        Assert.True(handler.Called);
    }
    private sealed class ProviderHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Called = true;
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
            Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            Assert.Contains("untrusted", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            { choices = new[] { new { finish_reason = "stop", message = new { content = "{\"type\":\"procedure\",\"confidence\":0.9}" } } } }), Encoding.UTF8, "application/json") };
        }
    }
    private static VaultDocument Snapshot(string content) => new("Procedures/deploy-coolify.md", content,
        MetadataJson.Hash(content), RevisionTokens.Create("Procedures/deploy-coolify.md", MetadataJson.Hash(content)), DateTimeOffset.Parse("2026-10-16T12:00:00Z"));
    private static string Encode(MetadataPatch patch) => JsonSerializer.Serialize(patch, MetadataJson.Options);
}
