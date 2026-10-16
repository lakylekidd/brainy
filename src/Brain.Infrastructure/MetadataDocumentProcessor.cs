using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Brain.Application;
using Brain.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Brain.Infrastructure;

public sealed class MetadataDocumentProcessor(MetadataOptions options) : IMetadataDocumentProcessor
{
    private static readonly string[] Required = ["id", "title", "metadata_version"];
    private static readonly string[] Lists = ["tags", "domains", "systems", "services", "projects", "related_concepts"];
    private static readonly string[] Fields = ["id", "title", "type", "summary", "filename", "path", "created_at", "modified_at",
        "content_hash", "git_revision", "metadata_version", "metadata_generated", "environment", .. Lists];
    public string SchemaHash
    {
        get
        {
            MetadataJson.ValidateOptions(options);
            return MetadataJson.Hash("metadata-schema-v1\n" + options.SchemaVersion + "\n" + string.Join('\n', options.DocumentTypes.Order(StringComparer.Ordinal)));
        }
    }

    public MetadataInspection Inspect(VaultDocument document)
    {
        MetadataJson.ValidateOptions(options);
        var parsed = Parse(document.Content);
        var hash = MetadataJson.Hash(parsed.Body);
        var issues = parsed.Issues.ToList();
        var valid = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!parsed.HasFrontmatter) issues.Add(new("missing_frontmatter", null, "Document has no frontmatter."));
        foreach (var name in Required.Where(name => !parsed.Nodes.ContainsKey(name)))
            issues.Add(new("missing_field", name, "Required metadata field is missing."));
        var editable = parsed.CanEdit;
        foreach (var (name, node) in parsed.Nodes.Where(n => Fields.Contains(n.Key, StringComparer.Ordinal)))
        {
            var value = Value(name, node);
            if (name == "metadata_version" && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var version))
            {
                if (version > options.SchemaVersion)
                { issues.Add(new("future_schema", name, "Document uses a newer metadata schema; automatic downgrade is prohibited.")); editable = false; continue; }
                if (version > 0 && version < options.SchemaVersion)
                { issues.Add(new("outdated_schema", name, "Metadata schema version is outdated.")); continue; }
            }
            if (Valid(name, value, document.Path, hash)) valid[name] = value;
            else issues.Add(new(name == "content_hash" ? "stale_content_hash" : "invalid_field", name, "Metadata field is invalid or stale under the current schema."));
        }
        return new(parsed.Body, hash, issues, editable, valid);
    }

    public MetadataPatch BuildPatch(VaultDocument document, DocumentProvenance provenance, SemanticMetadata? semantic)
    {
        var inspection = Inspect(document);
        Validation.Require(inspection.CanEdit, "Ambiguous or unsupported frontmatter requires manual repair.");
        var file = document.Path.Split('/')[^1];
        var stem = file[..^3];
        var slug = Regex.Replace(stem.Normalize(NormalizationForm.FormKD).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "document";
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        var title = Regex.Replace(stem.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").Trim();
        if (title.Length == 0) title = file;
        title = char.ToUpperInvariant(title[0]) + title[1..];
        if (title.Length > 300) title = title[..300];
        var candidates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["id"] = JsonSerializer.SerializeToElement(slug + "-" + MetadataJson.Hash(document.Path)[..12]),
            ["title"] = JsonSerializer.SerializeToElement(title), ["filename"] = JsonSerializer.SerializeToElement(file),
            ["path"] = JsonSerializer.SerializeToElement(document.Path), ["modified_at"] = JsonSerializer.SerializeToElement(document.ModifiedAt.ToString("O")),
            ["content_hash"] = JsonSerializer.SerializeToElement(inspection.BodyHash),
            ["metadata_version"] = JsonSerializer.SerializeToElement(options.SchemaVersion), ["metadata_generated"] = JsonSerializer.SerializeToElement(true)
        };
        if (provenance.GitRevision is not null) candidates["git_revision"] = JsonSerializer.SerializeToElement(provenance.GitRevision);
        if (provenance.CreatedAt is not null) candidates["created_at"] = JsonSerializer.SerializeToElement(provenance.CreatedAt.Value.ToString("O"));
        if (semantic is not null)
        {
            // Validate even implementations that construct the DTO directly; model output is never trusted.
            semantic = MetadataJson.ParseSemantic(JsonSerializer.Serialize(semantic, MetadataJson.Options), options);
            foreach (var property in JsonSerializer.SerializeToElement(semantic, MetadataJson.Options).EnumerateObject())
            {
                if (property.Name == "confidence" || property.Value.ValueKind == JsonValueKind.Null) continue;
                candidates[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.SerializeToElement(property.Value.EnumerateArray().Select(x => x.GetString()!.Trim()).ToArray())
                    : JsonSerializer.SerializeToElement(property.Value.GetString()!.Trim());
            }
        }
        var patch = new MetadataPatch(options.SchemaVersion, SchemaHash,
            candidates.Where(p => !inspection.ValidFields.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        _ = Apply(document, JsonSerializer.Serialize(patch, MetadataJson.Options));
        return patch;
    }

    public string Apply(VaultDocument document, string patchJson)
    {
        MetadataPatch patch;
        try
        {
            MetadataJson.RejectDuplicates(patchJson);
            patch = JsonSerializer.Deserialize<MetadataPatch>(patchJson, MetadataJson.Options) ?? throw new JsonException();
        }
        catch (JsonException) { throw new BrainException(422, "Invalid metadata patch JSON."); }
        Validation.Require(patch.SchemaVersion == options.SchemaVersion && patch.SchemaHash == SchemaHash,
            "Metadata schema changed after generation; scan and review a new proposal.");
        var inspection = Inspect(document);
        Validation.Require(inspection.CanEdit, "Ambiguous or unsupported frontmatter requires manual repair.");
        Validation.Require(patch.Fields is { Count: > 0 and <= 24 }, "Metadata patch must contain supported fields.");
        var parsed = Parse(document.Content);
        var raw = parsed.Raw;
        var spans = ValueSpans(raw);
        var edits = new List<(int Start, int End, string Text)>();
        var additions = new List<string>();
        var newline = document.Content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        foreach (var (name, value) in patch.Fields!)
        {
            Validation.Require(Fields.Contains(name, StringComparer.Ordinal) && Valid(name, value, document.Path, inspection.BodyHash), "Invalid generated metadata field.");
            Validation.Require(!inspection.ValidFields.TryGetValue(name, out var existing) || JsonElement.DeepEquals(existing, value),
                "Valid human metadata cannot be overwritten by enrichment.");
            var rendered = value.GetRawText(); // JSON scalar/array syntax is an escaped, safe YAML subset.
            if (parsed.Nodes.ContainsKey(name))
            {
                var (start, end) = spans[name];
                Validation.Require(start >= 0 && end >= start && end <= raw.Length, "Invalid frontmatter field boundaries.");
                var old = raw[start..end];
                if (start > 0 && raw[start - 1] == ':') rendered = " " + rendered;
                if (old.EndsWith('\n')) rendered += newline;
                edits.Add((start, end, rendered));
            }
            else additions.Add(name + ": " + rendered + newline);
        }
        foreach (var edit in edits.OrderByDescending(e => e.Start)) raw = raw[..edit.Start] + edit.Text + raw[edit.End..];
        if (additions.Count > 0) raw += (raw.Length > 0 && !raw.EndsWith('\n') ? newline : "") + string.Concat(additions);
        var result = parsed.HasFrontmatter
            ? parsed.Opening + raw + parsed.Closing + parsed.Body
            : "---" + newline + raw + "---" + newline + parsed.Body;
        var checkedResult = Inspect(document with { Content = result });
        Validation.Require(checkedResult.CanEdit && checkedResult.Issues.Count == 0 && checkedResult.Body == inspection.Body,
            "Generated metadata is incomplete or invalid; manual repair or semantic enrichment is needed.");
        return result;
    }

    private bool Valid(string name, JsonElement value, string path, string bodyHash)
    {
        if (name == "metadata_version") return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var version) && version == options.SchemaVersion;
        if (name == "metadata_generated") return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
        if (Lists.Contains(name, StringComparer.Ordinal))
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 30) return false;
            var values = value.EnumerateArray().ToArray();
            return values.All(v => v.ValueKind == JsonValueKind.String && Text(v.GetString(), 100, false)) &&
                values.Select(v => v.GetString()).Distinct(StringComparer.Ordinal).Count() == values.Length;
        }
        if (value.ValueKind != JsonValueKind.String || !Text(value.GetString(), name == "summary" ? 1000 : name == "title" ? 300 : 1000, true)) return false;
        var text = value.GetString()!;
        return name switch
        {
            "id" => Regex.IsMatch(text, @"\A[a-z0-9][a-z0-9-]{0,99}\z"),
            "type" => options.DocumentTypes.Contains(text, StringComparer.Ordinal),
            "path" => text == path, "filename" => text == path.Split('/')[^1], "content_hash" => text == bodyHash,
            "git_revision" => Regex.IsMatch(text, @"\A[a-f0-9]{40,64}\z"),
            "created_at" or "modified_at" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
            "environment" => Text(text, 100, false), _ => true
        };
    }
    private static bool Text(string? value, int max, bool multiline) => !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        !value.Any(c => char.IsControl(c) && (!multiline || c is not '\n' and not '\r' and not '\t'));

    private static JsonElement Value(string name, YamlNode node)
    {
        if (Lists.Contains(name, StringComparer.Ordinal))
            return node is YamlSequenceNode sequence && sequence.Children.All(n => n is YamlScalarNode s && StringScalar(s))
                ? JsonSerializer.SerializeToElement(sequence.Children.Cast<YamlScalarNode>().Select(s => s.Value).ToArray()) : default;
        if (node is not YamlScalarNode scalar || Null(scalar)) return default;
        if (name == "metadata_version") return scalar.Style == ScalarStyle.Plain && int.TryParse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? JsonSerializer.SerializeToElement(number) : default;
        if (name == "metadata_generated") return scalar.Style == ScalarStyle.Plain && bool.TryParse(scalar.Value, out var flag)
            ? JsonSerializer.SerializeToElement(flag) : default;
        return StringScalar(scalar) ? JsonSerializer.SerializeToElement(scalar.Value) : default;
    }
    private static bool StringScalar(YamlScalarNode scalar) => !Null(scalar) && (scalar.Style != ScalarStyle.Plain ||
        (!bool.TryParse(scalar.Value, out _) && !Regex.IsMatch(scalar.Value!, @"\A[-+]?(?:0[xob][0-9a-fA-F_]+|(?:[0-9][0-9_]*(?:\.[0-9_]*)?|\.[0-9_]+)(?:[eE][-+]?[0-9]+)?|\.(?:inf|Inf|INF|nan|NaN|NAN))\z")));
    private static bool Null(YamlScalarNode scalar) => scalar.Value is null ||
        (scalar.Style == ScalarStyle.Plain && scalar.Value is "" or "~" or "null" or "Null" or "NULL");

    private static Dictionary<string, (int Start, int End)> ValueSpans(string raw)
    {
        var result = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        var parser = new Parser(new StringReader(raw));
        parser.Consume<StreamStart>();
        if (!parser.TryConsume<DocumentStart>(out _)) return result;
        if (!parser.TryConsume<MappingStart>(out _)) return result;
        while (!parser.Accept<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            result.Add(key, ReadSpan(parser, raw));
        }
        return result;
        static (int Start, int End) ReadSpan(IParser parser, string raw)
        {
            var node = parser.Consume<ParsingEvent>();
            var start = checked((int)node.Start.Index);
            if (node is Scalar) return (start, checked((int)node.End.Index));
            var sequence = node is SequenceStart;
            if (!sequence && node is not MappingStart) throw new YamlException("Unsupported node.");
            while (!(sequence ? parser.Accept<SequenceEnd>(out _) : parser.Accept<MappingEnd>(out _))) _ = ReadSpan(parser, raw);
            var end = parser.Consume<ParsingEvent>();
            var endIndex = checked((int)end.End.Index);
            var flow = node is SequenceStart { Style: SequenceStyle.Flow } or MappingStart { Style: MappingStyle.Flow };
            // YamlDotNet's flow-end event mark points at the closing delimiter itself.
            if (flow && endIndex < raw.Length && raw[endIndex] == (sequence ? ']' : '}')) endIndex++;
            return (start, endIndex);
        }
    }

    private sealed record Parsed(bool HasFrontmatter, bool CanEdit, string Opening, string Raw, string Closing, string Body,
        Dictionary<string, YamlNode> Nodes, IReadOnlyList<MetadataIssue> Issues);
    private static Parsed Parse(string content)
    {
        var opening = Regex.Match(content, @"\A---[ \t]*(?:\r?\n|$)");
        if (!opening.Success) return new(false, true, "", "", "", content, new(StringComparer.Ordinal), []);
        var closing = Regex.Match(content[opening.Length..], @"(?m)^(?:---|\.\.\.)[ \t]*(?:\r?\n|$)");
        Parsed Invalid(string code, string message) => new(true, false, "", "", "", content, new(StringComparer.Ordinal), [new(code, null, message)]);
        if (!closing.Success) return Invalid("invalid_yaml", "Frontmatter has no closing delimiter; repair it manually.");
        var raw = content.Substring(opening.Length, closing.Index);
        if (raw.Length > 65536) return Invalid("invalid_yaml", "Frontmatter exceeds 65,536 characters.");
        var body = content[(opening.Length + closing.Index + closing.Length)..];
        try
        {
            var parser = new Parser(new StringReader(raw));
            var count = 0; var depth = 0;
            while (parser.MoveNext())
            {
                if (++count > 4096) throw new YamlException("Too many YAML nodes.");
                if (parser.Current is AnchorAlias || parser.Current is NodeEvent n && (!n.Anchor.IsEmpty || !n.Tag.IsEmpty)) throw new YamlException("YAML aliases, anchors and tags require manual review.");
                if (parser.Current is MappingStart or SequenceStart) { if (++depth > 16) throw new YamlException("YAML nesting is too deep."); }
                if (parser.Current is MappingEnd or SequenceEnd) depth--;
            }
            var stream = new YamlStream(); stream.Load(new StringReader(raw));
            if (stream.Documents.Count > 1) throw new YamlException("Multiple YAML documents.");
            var nodes = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
            if (stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode map)
            {
                // Block mappings permit replacing only changed value spans, preserving comments/styles/unknown fields.
                if (map.Style == MappingStyle.Flow) return Invalid("unsupported_yaml", "Flow-style frontmatter requires manual conversion to a block mapping.");
                foreach (var pair in map.Children)
                {
                    if (pair.Key is not YamlScalarNode { Value: { } key } || key == "<<" || !nodes.TryAdd(key, pair.Value)) throw new YamlException("Invalid metadata key.");
                }
            }
            else if (stream.Documents.Count == 1 && stream.Documents[0].RootNode is not YamlScalarNode { Value: "" or null })
                throw new YamlException("Frontmatter must be a mapping.");
            return new(true, true, opening.Value, raw, closing.Value, body, nodes, []);
        }
        catch (YamlException) { return Invalid("invalid_yaml", "Frontmatter YAML is invalid or unsupported; preserve it and repair it manually."); }
    }
}
