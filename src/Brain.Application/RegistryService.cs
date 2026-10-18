using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Brain.Domain;

namespace Brain.Application;

public sealed class RegistryService(IRegistrySource source, IRegistryIndex index, IEmbeddingProvider embeddings,
    IAuditLog audit, TimeProvider time, IActivityPublisher activity)
{
    public Task<IReadOnlyList<RegistryResult>> ListAsync(AgentContext actor, RegistryKind kind, CancellationToken ct = default) =>
        activity.RunAsync(new("brain.registry.list", "brain.registry.started", "brain.registry.completed", "brain.registry.failed",
            $"agent:{actor.Id}", "brain", actor.Id), () => ListCoreAsync(actor, kind, ct), ct);

    private async Task<IReadOnlyList<RegistryResult>> ListCoreAsync(AgentContext actor, RegistryKind kind, CancellationToken ct)
    {
        actor.Require(Permissions.RegistryRead);
        var entries = await source.ReadAsync(ct);
        var allowed = await AuthorizeAsync(actor, entries, ct);
        var results = allowed.Where(e => e.Kind == kind).OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => View(e)).ToArray();
        await DiscoveredAsync(actor, results.Select(r => $"{r.Type}:{r.Id}"), ct);
        return results;
    }

    public Task<SkillView> GetSkillAsync(AgentContext actor, string id, CancellationToken ct = default) =>
        activity.RunAsync(new("brain.skill.read", "brain.skill.read.started", "brain.skill.read.completed", "brain.skill.read.failed",
            $"agent:{actor.Id}", $"skill:{id}", actor.Id), async () =>
        {
            var entry = await GetAsync(actor, RegistryKind.Skill, id, ct);
            activity.Publish("skill.selected", ActivityStatus.Succeeded, $"agent:{actor.Id}", entry.Key);
            foreach (var tool in entry.RequiredTools)
                activity.Publish("brain.capability.discovered", ActivityStatus.Succeeded, entry.Key, $"tool:{tool}");
            return new SkillView(View(entry), entry.Instructions!,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Instructions!))));
        }, ct);

    public Task<RegistryResult> GetToolAsync(AgentContext actor, string id, CancellationToken ct = default) =>
        activity.RunAsync(new("brain.tool.read", "brain.registry.started", "brain.registry.completed", "brain.registry.failed",
            $"agent:{actor.Id}", $"tool:{id}", actor.Id), async () => View(await GetAsync(actor, RegistryKind.Tool, id, ct)), ct);

    private async Task<RegistryDefinition> GetAsync(AgentContext actor, RegistryKind kind, string id, CancellationToken ct)
    {
        actor.Require(Permissions.RegistryRead);
        Validation.Text(id, "Id", 100);
        var entries = await source.ReadAsync(ct);
        var allowed = await AuthorizeAsync(actor, entries, ct);
        var entry = allowed.SingleOrDefault(e => e.Kind == kind && e.Id == id)
            ?? throw new BrainException(404, "Capability not found.");
        await DiscoveredAsync(actor, [entry.Key], ct);
        return entry;
    }

    public Task<IReadOnlyList<RegistryResult>> SearchAsync(AgentContext actor, RegistryKind kind,
        RegistrySearchRequest request, CancellationToken ct = default) =>
        activity.RunAsync(new("brain.registry.search", "brain.search.started", "brain.search.completed", "brain.search.failed",
            $"agent:{actor.Id}", "brain", actor.Id), () => SearchResultsAsync(actor, kind, request, ct), ct);

    private async Task<IReadOnlyList<RegistryResult>> SearchResultsAsync(AgentContext actor, RegistryKind kind,
        RegistrySearchRequest request, CancellationToken ct = default)
    {
        var catalog = await SearchCoreAsync(actor, request, ct);
        var ranked = await RankAsync(catalog.Where(e => e.Kind == kind).ToArray(), request,
            await EmbedAsync(request.Query, ct), ct);
        var current = await CurrentKeysAsync(catalog, ct);
        var results = ranked.Where(r => current.Contains($"{r.Type}:{r.Id}")).ToArray();
        await DiscoveredAsync(actor, results.Select(r => $"{r.Type}:{r.Id}"), ct);
        return results;
    }

    public Task<CapabilitySearchResult> FindCapabilitiesAsync(AgentContext actor, RegistrySearchRequest request, CancellationToken ct = default) =>
        activity.RunAsync(new("brain.capabilities.search", "brain.search.started", "brain.search.completed", "brain.search.failed",
            $"agent:{actor.Id}", "brain", actor.Id), () => FindCoreAsync(actor, request, ct), ct);

    private async Task<CapabilitySearchResult> FindCoreAsync(AgentContext actor, RegistrySearchRequest request, CancellationToken ct)
    {
        var catalog = await SearchCoreAsync(actor, request, ct);
        var vector = await EmbedAsync(request.Query, ct);
        var skills = await RankAsync(catalog.Where(e => e.Kind == RegistryKind.Skill).ToArray(), request, vector, ct);
        var tools = (await RankAsync(catalog.Where(e => e.Kind == RegistryKind.Tool).ToArray(), request, vector, ct)).ToList();
        // Include every dependency of a returned skill, even when its own description ranks lower.
        foreach (var skill in skills)
            foreach (var id in skill.Metadata.RequiredTools)
                if (tools.All(t => t.Id != id))
                    tools.Add(View(catalog.Single(t => t.Kind == RegistryKind.Tool && t.Id == id)));
        var current = await CurrentKeysAsync(catalog, ct);
        var currentSkills = skills.Where(r => current.Contains($"skill:{r.Id}")).ToArray();
        var currentTools = tools.Where(r => current.Contains($"tool:{r.Id}")).ToArray();
        await DiscoveredAsync(actor, currentSkills.Concat(currentTools).Select(r => $"{r.Type}:{r.Id}"), ct);
        return new(currentSkills, currentTools);
    }

    private async Task<HashSet<string>> CurrentKeysAsync(IReadOnlyList<RegistryDefinition> catalog, CancellationToken ct)
    {
        // Embedding and database calls may take time. Recheck canonical hashes before returning any hit.
        var latest = (await source.ReadAsync(ct)).Select(e => e.Fingerprint).ToHashSet(StringComparer.Ordinal);
        var tools = catalog.Where(e => e.Kind == RegistryKind.Tool && latest.Contains(e.Fingerprint)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return catalog.Where(e => latest.Contains(e.Fingerprint) && e.RequiredTools.All(tools.Contains)).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<RegistryDefinition>> SearchCoreAsync(AgentContext actor, RegistrySearchRequest request, CancellationToken ct)
    {
        actor.Require(Permissions.RegistryRead);
        Validation.Text(request.Query, "Query", 4000);
        Validation.Require(request.Limit is >= 1 and <= 100, "Limit must be between 1 and 100.");
        Validation.Require(request.Tags is null || (request.Tags.Length <= 20 && request.Tags.All(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 100)), "Invalid tags.");
        var entries = await source.ReadAsync(ct);
        var allowed = await AuthorizeAsync(actor, entries, ct);
        var current = (await index.StatusAsync(ct)).Where(s => s.Model == embeddings.Model).Select(s => s.Fingerprint).ToHashSet(StringComparer.Ordinal);
        // A skill's tool dependencies must also have a current index. Permission checks always use live manifests.
        var freshTools = allowed.Where(e => e.Kind == RegistryKind.Tool && current.Contains(e.Fingerprint)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return allowed.Where(e => current.Contains(e.Fingerprint) && e.RequiredTools.All(freshTools.Contains)).ToArray();
    }

    private async Task<IReadOnlyList<RegistryResult>> RankAsync(IReadOnlyList<RegistryDefinition> entries,
        RegistrySearchRequest request, float[] vector, CancellationToken ct)
    {
        var candidates = entries.Where(e => request.Tags is null || request.Tags.All(t => e.Tags.Contains(t, StringComparer.Ordinal))).ToDictionary(e => e.Fingerprint, StringComparer.Ordinal);
        if (candidates.Count == 0) return [];
        // Restrict candidates before SQL LIMIT, so denied high-scoring entries cannot crowd out allowed ones.
        var hits = await index.SearchAsync(candidates.Keys.ToArray(), vector, embeddings.Model, request.Limit, ct);
        return hits.Select(h => View(candidates[h.Fingerprint], h.Score)).ToArray();
    }

    private async Task<IReadOnlyList<RegistryDefinition>> AuthorizeAsync(AgentContext actor, IReadOnlyList<RegistryDefinition> entries, CancellationToken ct)
    {
        var keys = RegistryAccess.AllowedKeys(actor, entries);
        var allowed = new List<RegistryDefinition>();
        foreach (var entry in entries)
        {
            if (keys.Contains(entry.Key)) allowed.Add(entry);
            else
            {
                await audit.WriteAsync(Event(actor.Id, "brain.capability.denied", entry.Key, "denied"), ct);
                activity.Publish("brain.capability.denied", ActivityStatus.Failed, $"agent:{actor.Id}", entry.Key);
            }
        }
        return allowed;
    }

    public async Task<RegistryScanResult> ScanAsync(AgentContext actor, CancellationToken ct = default)
    {
        actor.Require(Permissions.RegistryAdmin);
        return await ScanCoreAsync(actor.Id, ct);
    }

    public async Task<RegistryScanResult> ScanCoreAsync(string actor, CancellationToken ct = default)
    {
        await using var lease = await index.TryAcquireScanAsync(ct);
        if (lease is null) return new(0, 0, 0, true);
        var entries = await source.ReadAsync(ct);
        var previous = await index.StatusAsync(ct);
        var unchanged = entries.Count(e => previous.Any(p => p.Fingerprint == e.Fingerprint && p.Model == embeddings.Model));
        var removed = previous.Count(p => entries.All(e => !p.Fingerprint.StartsWith(e.Key + "@", StringComparison.Ordinal)));
        if (unchanged == entries.Count && previous.Count == entries.Count) return new(0, unchanged, 0);
        var embedded = new List<EmbeddedRegistryDefinition>();
        foreach (var entry in entries)
            embedded.Add(new(entry, await EmbedAsync(string.Join('\n', entry.Name, entry.Description,
                string.Join(' ', entry.Tags), string.Join(' ', entry.Capabilities)), ct)));
        var latest = await source.ReadAsync(ct);
        if (!latest.Select(e => e.Fingerprint).Order().SequenceEqual(entries.Select(e => e.Fingerprint).Order()))
            throw new BrainException(409, "Registry changed during indexing; retry the scan.");
        await index.ReplaceAsync(embedded, embeddings.Model, time.GetUtcNow(), Event(actor, "brain.registry.indexed", "registry"), ct);
        using (var scope = activity.Start(new("brain.registry.index", "brain.registry.scan.started",
                   "brain.registry.scan.completed", "brain.registry.scan.failed", "registry", "registry", actor)))
            activity.Publish("brain.registry.indexed", ActivityStatus.Succeeded, "registry", "registry");
        return new(entries.Count, 0, removed);
    }

    private async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var vector = await embeddings.EmbedAsync(text, ct);
        if (embeddings.Model.Dimensions != 384 || vector.Length != embeddings.Model.Dimensions ||
            vector.Any(v => !float.IsFinite(v)) || !vector.Any(v => v != 0))
            throw new InvalidOperationException("Embedding provider returned an invalid vector or unsupported dimensions.");
        return vector;
    }

    private async Task DiscoveredAsync(AgentContext actor, IEnumerable<string> keys, CancellationToken ct)
    {
        var ids = keys.ToArray();
        await audit.WriteAsync(new AuditEvent { AgentId = actor.Id, Action = "brain.capability.discovered", Resource = "registry",
            Timestamp = time.GetUtcNow(), MetadataJson = JsonSerializer.Serialize(new { capabilities = ids }) }, ct);
        foreach (var id in ids)
            activity.Publish("brain.capability.discovered", ActivityStatus.Succeeded, $"agent:{actor.Id}", id);
    }

    private AuditEvent Event(string actor, string action, string resource, string result = "success") =>
        new() { AgentId = actor, Action = action, Resource = resource, Result = result, Timestamp = time.GetUtcNow() };

    private static RegistryResult View(RegistryDefinition entry, double? score = null) => new(
        entry.Kind == RegistryKind.Skill ? "skill" : "tool", entry.Id, entry.Name, entry.Description, score, entry.Source,
        new(entry.Tags, entry.RequiredPermissions, entry.RequiredTools, entry.Provider, entry.Capabilities,
            entry.ContentHash, entry.RuntimeRequirements ?? []));
}
