using Brain.Domain;

namespace Brain.Application;

public static class RegistryAccess
{
    public static IReadOnlySet<string> AllowedKeys(AgentContext actor, IReadOnlyList<RegistryDefinition> entries)
    {
        var tools = entries.Where(e => e.Kind == RegistryKind.Tool && e.RequiredPermissions.All(actor.Has))
            .Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return entries.Where(e => e.RequiredPermissions.All(actor.Has) && e.RequiredTools.All(tools.Contains))
            .Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
    }
}
