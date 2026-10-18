using Brain.Domain;

namespace Brain.Application;

public interface IRegistrySource
{
    Task<IReadOnlyList<RegistryDefinition>> ReadAsync(CancellationToken ct);
}

public sealed record RegistrySearchRequest(string Query, string[]? Tags = null, int Limit = 10);
public sealed record RegistryMetadata(string[] Tags, string[] RequiredPermissions, string[] RequiredTools,
    string? Provider, string[] Capabilities, string ContentHash, string[]? RuntimeRequirements = null);
public sealed record RegistryResult(string Type, string Id, string Title, string Content,
    double? Score, string Source, RegistryMetadata Metadata);
public sealed record SkillView(RegistryResult Skill, string Instructions, string InstructionsHash);
public sealed record CapabilitySearchResult(IReadOnlyList<RegistryResult> Skills, IReadOnlyList<RegistryResult> Tools);
public sealed record RegistryScanResult(int Indexed, int Unchanged, int Removed, bool AlreadyRunning = false);
public sealed record RegistryIndexStatus(string Fingerprint, EmbeddingModel Model);
public sealed record EmbeddedRegistryDefinition(RegistryDefinition Definition, float[] Embedding);
public sealed record RegistryHit(string Fingerprint, double Score);

public interface IRegistryIndex
{
    Task<IAsyncDisposable?> TryAcquireScanAsync(CancellationToken ct);
    Task<IReadOnlyList<RegistryIndexStatus>> StatusAsync(CancellationToken ct);
    Task ReplaceAsync(IReadOnlyList<EmbeddedRegistryDefinition> entries, EmbeddingModel model,
        DateTimeOffset indexedAt, AuditEvent audit, CancellationToken ct);
    Task<IReadOnlyList<RegistryHit>> SearchAsync(string[] allowedFingerprints, float[] embedding,
        EmbeddingModel model, int limit, CancellationToken ct);
}
