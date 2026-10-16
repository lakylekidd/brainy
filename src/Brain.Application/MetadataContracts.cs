using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public sealed class MetadataOptions
{
    public bool Enabled { get; set; }
    public string AgentId { get; set; } = "metadata-enrichment-worker";
    public int SchemaVersion { get; set; } = 1;
    public string[] DocumentTypes { get; set; } = ["procedure", "architecture", "service", "incident", "decision", "project", "reference", "note", "runbook"];
    public bool SemanticEnabled { get; set; }
}

public sealed record MetadataIssue(string Code, string? Field, string Message);
public sealed record MetadataInspection(string Body, string BodyHash, IReadOnlyList<MetadataIssue> Issues,
    bool CanEdit, IReadOnlyDictionary<string, JsonElement> ValidFields);
public sealed record DocumentProvenance(string? GitRevision = null, DateTimeOffset? CreatedAt = null);
public sealed record MetadataPatch(int SchemaVersion, string SchemaHash, Dictionary<string, JsonElement> Fields);
public sealed record MetadataProviderInfo(string Provider, string Model, string ConfigurationKey);
public sealed record MetadataEnrichmentInput(string Path, string Body, IReadOnlyDictionary<string, JsonElement> ExistingFields, string[] AllowedDocumentTypes);
public sealed record MetadataScanResult(int Inspected, int Skipped, int Issues, int Proposed, int Failed, int Removed, bool AlreadyRunning = false);
public sealed record MetadataIssueView(string Path, string RevisionToken, int SchemaVersion, IReadOnlyList<MetadataIssue> Issues,
    Guid? ProposalId, DateTimeOffset CheckedAt, DateTimeOffset? LastEnrichedAt, string? Provider, string? Model, string? LastError);

public interface IMetadataDocumentProcessor
{
    string SchemaHash { get; }
    MetadataInspection Inspect(VaultDocument document);
    MetadataPatch BuildPatch(VaultDocument document, DocumentProvenance provenance, SemanticMetadata? semantic);
    string Apply(VaultDocument document, string patchJson);
}
public interface IKnowledgeProvenance
{
    Task<DocumentProvenance> ReadAsync(string path, CancellationToken ct);
}
public interface IMetadataEnrichmentProvider
{
    MetadataProviderInfo Info { get; }
    Task<string> GenerateJsonAsync(MetadataEnrichmentInput input, CancellationToken ct);
}
public interface IMetadataScanStore
{
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct);
    Task<MetadataScanState?> FindAsync(string path, CancellationToken ct);
    Task<IReadOnlyList<MetadataScanState>> IssuesAsync(int limit, CancellationToken ct);
    Task SaveAsync(MetadataScanState state, KnowledgeWriteProposal? proposal, IReadOnlyList<AuditEvent> events, CancellationToken ct);
    Task<int> RemoveMissingAsync(IReadOnlyList<string> paths, CancellationToken ct);
}

public sealed record SemanticMetadata
{
    public string? Type { get; init; }
    public string? Summary { get; init; }
    public string[]? Tags { get; init; }
    public string[]? Domains { get; init; }
    public string[]? Systems { get; init; }
    public string[]? Services { get; init; }
    public string[]? Projects { get; init; }
    public string? Environment { get; init; }
    public string[]? RelatedConcepts { get; init; }
    [JsonRequired] public double Confidence { get; init; }
}

public static class MetadataJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static void ValidateOptions(MetadataOptions options)
    {
        if (options.SchemaVersion < 1 || options.DocumentTypes is not { Length: > 0 and <= 100 } ||
            options.DocumentTypes.Any(t => t is null || !Regex.IsMatch(t, @"\A[a-z][a-z0-9_-]{0,49}\z")) ||
            options.DocumentTypes.Distinct(StringComparer.Ordinal).Count() != options.DocumentTypes.Length)
            throw new InvalidOperationException("Metadata schema version and document types must be valid.");
    }
    public static SemanticMetadata ParseSemantic(string json, MetadataOptions options)
    {
        try
        {
            Validation.Require(json.Length <= 16384, "Semantic metadata exceeds 16,384 characters.");
            RejectDuplicates(json);
            var value = JsonSerializer.Deserialize<SemanticMetadata>(json, Options) ?? throw new JsonException();
            Validation.Require(double.IsFinite(value.Confidence) && value.Confidence is >= 0 and <= 1, "Invalid semantic confidence.");
            Validation.Require(value.Type is null || options.DocumentTypes.Contains(value.Type, StringComparer.Ordinal), "Invalid semantic document type.");
            if (value.Summary is not null) Validation.Text(value.Summary, "Summary", 1000);
            if (value.Environment is not null) Validation.Text(value.Environment, "Environment", 100);
            foreach (var list in new[] { value.Tags, value.Domains, value.Systems, value.Services, value.Projects, value.RelatedConcepts })
                Validation.Require(list is null || (list.Length <= 30 && list.All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 100 && !s.Any(char.IsControl)) &&
                    list.Select(s => s.Trim()).Distinct(StringComparer.Ordinal).Count() == list.Length), "Invalid semantic list.");
            return value;
        }
        catch (Exception e) when (e is JsonException or BrainException)
        { throw new BrainException(422, "Semantic provider returned malformed or invalid metadata JSON."); }
    }
    public static void RejectDuplicates(string json)
    {
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
        Check(document.RootElement);
        static void Check(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property."); Check(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) Check(value);
        }
    }
}
