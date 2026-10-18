using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public sealed record DependencyView(string SkillId, string Requirement, string EnvironmentKind,
    string? EnvironmentId, bool Available, string Reason);

public sealed class DependencyService(RegistryService registry, RuntimeService runtime)
{
    private static readonly Regex Requirement = new(
        @"\A(?<kind>host|container|remote|harness):(?<name>[a-z][a-z0-9_.-]{0,99})(?:>=(?<version>\d+(?:\.\d+){0,3}))?\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){0,3}", RegexOptions.Compiled);

    public async Task<IReadOnlyList<DependencyView>> CheckAsync(AgentContext actor, Guid? ownDeviceId,
        Guid deviceId, CancellationToken ct)
    {
        actor.Require(Permissions.RuntimeRead);
        var environments = await runtime.ListAsync(actor, ownDeviceId, deviceId, ct);
        var skills = await registry.ListAsync(actor, RegistryKind.Skill, ct);
        var results = new List<DependencyView>();
        foreach (var skill in skills)
            foreach (var raw in skill.Metadata.RuntimeRequirements ?? [])
            {
                var requirement = Requirement.Match(raw);
                if (!requirement.Success) continue; // Source parser validates this grammar.
                var kind = requirement.Groups["kind"].Value;
                var name = requirement.Groups["name"].Value;
                var minimum = requirement.Groups["version"].Value;
                var options = environments.Where(e => e.Kind.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase)).ToArray();
                var match = options.FirstOrDefault(e => Available(e, name, minimum));
                results.Add(new(skill.Id, raw, kind, match?.EnvironmentId,
                    match is not null, match is not null ? "available" : options.Length == 0 ? "environment not reported" : "missing or below minimum version"));
            }
        return results;
    }

    private static bool Available(RuntimeView environment, string name, string minimum)
    {
        if (!environment.Healthy) return false;
        if (name == "docker" && !environment.DockerAvailable) return false;
        if (!environment.Commands.TryGetValue(name, out var installed) || installed is null) return false;
        if (minimum.Length == 0) return true;
        var candidate = VersionPattern.Match(installed);
        return candidate.Success && TryParse(candidate.Value, out var current) &&
            TryParse(minimum, out var required) && current.CompareTo(required) >= 0;
    }

    private static bool TryParse(string value, out Version version)
    {
        var parts = value.Split('.');
        version = new Version(0, 0, 0, 0);
        if (parts.Length > 4) return false;
        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out numbers[i])) return false;
        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }
}
