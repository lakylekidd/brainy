using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brain.Client;

public sealed record SkillSyncState(string SourceHash, string InstructionsHash);

public static class SkillSynchronizer
{
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,105}$", RegexOptions.Compiled);
    public static string Root => Path.Combine(ClientState.DirectoryPath, "skills");

    public static async Task SyncAsync(HttpClient http, ClientState state, CancellationToken ct)
    {
        using var listResponse = await http.GetAsync(new Uri(state.Server, "/api/skills"), ct);
        listResponse.EnsureSuccessStatusCode();
        using var list = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var changed = 0;
        var conflicts = 0;
        var reports = new List<object>();
        foreach (var item in list.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            if (!IdPattern.IsMatch(id) || id.Length > 100) throw new InvalidOperationException("Brain returned an invalid skill ID.");
            using var response = await http.GetAsync(new Uri(state.Server, "/api/skills/" + id), ct);
            response.EnsureSuccessStatusCode();
            using var detail = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var instructions = detail.RootElement.GetProperty("instructions").GetString()!;
            var hash = detail.RootElement.GetProperty("instructionsHash").GetString()!;
            var computed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions)));
            if (!string.Equals(hash, computed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Checksum mismatch for skill {id}.");
            var sourceHash = detail.RootElement.GetProperty("skill").GetProperty("metadata")
                .GetProperty("contentHash").GetString()!;
            var result = await InstallAsync(id, instructions, new(sourceHash, computed), ct);
            reports.Add(new { harness = "brain", skillId = id, sourceHash,
                status = result == "conflict" ? "conflict" : "synced" });
            if (result == "conflict") { conflicts++; Console.WriteLine($"{id}: local changes; kept existing copy."); }
            else if (result == "updated") { changed++; Console.WriteLine($"{id}: synchronized."); }
            var skill = detail.RootElement.GetProperty("skill");
            var description = skill.GetProperty("content").GetString()!;
            var wrapped = instructions.StartsWith("---\n", StringComparison.Ordinal) ? instructions :
                $"---\nname: brain-{id}\ndescription: {JsonSerializer.Serialize(description.Replace('\n', ' '))}\n---\n\n{instructions}";
            var wrappedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(wrapped)));
            foreach (var harness in state.ConnectedHarnesses)
            {
                var target = HarnessRoot(harness);
                var installed = await InstallAsync("brain-" + id, wrapped,
                    new(sourceHash, wrappedHash), ct, target);
                reports.Add(new { harness, skillId = id, sourceHash,
                    status = installed == "conflict" ? "conflict" : "synced" });
                if (installed == "conflict") { conflicts++; Console.WriteLine($"{harness}/{id}: local changes; kept existing copy."); }
                else if (installed == "updated") changed++;
            }
        }
        using var syncResponse = await http.PostAsJsonAsync(new Uri(state.Server,
            $"/api/devices/{state.DeviceId}/skills/sync"), new { entries = reports }, ct);
        syncResponse.EnsureSuccessStatusCode();
        Console.WriteLine($"Skill sync: {changed} updated, {conflicts} conflicts.");
        if (conflicts > 0) throw new InvalidOperationException("Resolve local skill conflicts before synchronization can finish.");
    }

    private static string HarnessRoot(string harness) => harness switch
    {
        "claude-code" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills"),
        "codex" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "skills"),
        "hermes" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes", "skills"),
        _ => throw new InvalidOperationException("Unknown connected harness.")
    };

    public static async Task<string> InstallAsync(string id, string instructions, SkillSyncState revision,
        CancellationToken ct, string? rootPath = null)
    {
        if (!IdPattern.IsMatch(id)) throw new InvalidOperationException("Invalid skill ID.");
        var root = rootPath ?? Root;
        var parent = Path.GetDirectoryName(Path.GetFullPath(root))!;
        if (Directory.Exists(parent) && new DirectoryInfo(parent).LinkTarget is not null)
            throw new InvalidOperationException("Skill parent cannot be a link.");
        Directory.CreateDirectory(root);
        if (new DirectoryInfo(root).LinkTarget is not null) throw new InvalidOperationException("Skill root cannot be a link.");
        var directory = Path.Combine(root, id);
        if (Directory.Exists(directory) && new DirectoryInfo(directory).LinkTarget is not null)
            throw new InvalidOperationException("Skill directory cannot be a link.");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "SKILL.md");
        var stateFile = Path.Combine(directory, ".brain-sync.json");
        if (File.Exists(file) && new FileInfo(file).LinkTarget is not null ||
            File.Exists(stateFile) && new FileInfo(stateFile).LinkTarget is not null)
            throw new InvalidOperationException("Skill files cannot be links.");
        SkillSyncState? previous = null;
        if (File.Exists(stateFile)) previous = JsonSerializer.Deserialize<SkillSyncState>(await File.ReadAllTextAsync(stateFile, ct));
        if (File.Exists(file))
        {
            var localHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file, ct)));
            if (previous is null || !string.Equals(localHash, previous.InstructionsHash, StringComparison.OrdinalIgnoreCase))
                return "conflict";
            if (previous.SourceHash == revision.SourceHash && localHash == revision.InstructionsHash) return "unchanged";
        }
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, instructions, new UTF8Encoding(false), ct);
        File.Move(temporary, file, true);
        temporary = stateFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(revision), ct);
        File.Move(temporary, stateFile, true);
        return "updated";
    }
}
