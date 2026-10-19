namespace Brain.Domain;

public enum RegistryKind { Skill, Tool }

// Canonical definitions come from the operator-managed agent-platform checkout.
// Instructions are returned only by get-skill, never treated as authorization policy.
public sealed record RegistryDefinition(RegistryKind Kind, string Id, string Name, string Description,
    string[] Tags, string[] RequiredPermissions, string[] RequiredTools, string? Provider,
    string[] Capabilities, string Source, string ContentHash, string? Instructions = null,
    string[]? RuntimeRequirements = null)
{
    public string Key => $"{Kind.ToString().ToLowerInvariant()}:{Id}";
    public string Fingerprint => $"{Key}@{ContentHash}";
}
