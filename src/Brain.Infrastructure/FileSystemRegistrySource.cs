using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Brain.Application;
using Brain.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Brain.Infrastructure;

public sealed class RegistryOptions
{
    public string? RootPath { get; set; }
}

public sealed class FileSystemRegistrySource(RegistryOptions options) : IRegistrySource
{
    private static readonly Regex Identifier = new(@"\A[a-z0-9][a-z0-9-]{0,99}\z", RegexOptions.CultureInvariant);
    private static readonly Regex PermissionName = new(@"\A[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex CapabilityName = new(@"\A[a-z][a-z0-9_-]{0,99}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RuntimeRequirement = new(@"\A(host|container|remote|harness):([a-z][a-z0-9_.-]{0,99})(?:>=(\d+(?:\.\d+){0,3}))?\z", RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public async Task<IReadOnlyList<RegistryDefinition>> ReadAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath)) throw new BrainException(503, "Registry:RootPath is not configured.");
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RootPath));
            CheckAncestors(root);
            var entries = new List<RegistryDefinition>();
            foreach (var kind in new[] { RegistryKind.Skill, RegistryKind.Tool })
            {
                var folder = Path.Combine(root, kind == RegistryKind.Skill ? "skills" : "tools");
                CheckPath(folder);
                // Exactly one directory per definition; do not follow links or recursively discover files.
                foreach (var directory in Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    CheckPath(directory);
                    var id = Path.GetFileName(directory);
                    Require(Identifier.IsMatch(id) && !IsDeviceName(id), "Invalid registry directory id.");
                    Require(entries.Count < 500, "Registry exceeds 500 definitions.");
                    var manifest = await ReadFileAsync(Path.Combine(directory, "manifest.yaml"), 16384, ct);
                    var instructions = kind == RegistryKind.Skill
                        ? await ReadFileAsync(Path.Combine(directory, "SKILL.md"), 65536, ct) : null;
                    if (instructions is not null) Require(!string.IsNullOrWhiteSpace(instructions), "SKILL.md must not be empty.");
                    var hash = Hash(Hash(manifest) + ":" + (instructions is null ? "" : Hash(instructions)));
                    entries.Add(Parse(kind, id, manifest, hash, instructions));
                }
            }
            Require(entries.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count() == entries.Count, "Duplicate registry id.");
            return entries;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        { throw new BrainException(503, "Registry source is unavailable or invalid UTF-8; discovery is unavailable until repaired."); }
    }

    public static RegistryDefinition Parse(RegistryKind kind, string directoryId, string yaml, string contentHash, string? instructions = null)
    {
        Require(yaml.Length <= 16384, "Manifest is too large.");
        try
        {
            var parser = new Parser(new StringReader(yaml));
            parser.Consume<StreamStart>();
            parser.Consume<DocumentStart>();
            CheckNode(parser.Consume<MappingStart>());
            var text = new Dictionary<string, string>(StringComparer.Ordinal);
            var lists = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            var scalarKeys = kind == RegistryKind.Skill ? new[] { "id", "name", "description" } : ["id", "name", "description", "provider"];
            var listKeys = kind == RegistryKind.Skill ? new[] { "tags", "required_tools", "required_permissions", "runtime_requirements" } : ["tags", "permissions", "capabilities"];
            while (!parser.Accept<MappingEnd>(out _))
            {
                var key = Scalar(parser, 100);
                Require(fields.Add(key), "Duplicate manifest field.");
                if (scalarKeys.Contains(key, StringComparer.Ordinal)) text[key] = Scalar(parser, key == "description" ? 4000 : 200);
                else if (listKeys.Contains(key, StringComparer.Ordinal))
                {
                    CheckNode(parser.Consume<SequenceStart>());
                    var values = new List<string>();
                    while (!parser.Accept<SequenceEnd>(out _))
                    {
                        Require(values.Count < 100, "Manifest list exceeds 100 values.");
                        values.Add(Scalar(parser, 100));
                    }
                    parser.Consume<SequenceEnd>();
                    Require(values.Distinct(StringComparer.Ordinal).Count() == values.Count, "Duplicate manifest list value.");
                    lists[key] = values.ToArray();
                }
                else throw new BrainException(422, "Unknown manifest field.");
            }
            parser.Consume<MappingEnd>();
            parser.Consume<DocumentEnd>();
            parser.Consume<StreamEnd>();
            Require(scalarKeys.All(text.ContainsKey) && listKeys.Where(k => k is not ("tags" or "runtime_requirements")).All(lists.ContainsKey), "Missing required manifest field.");
            Require(Identifier.IsMatch(text["id"]) && !IsDeviceName(text["id"]) && text["id"] == directoryId, "Manifest id must match its directory.");
            var tags = lists.GetValueOrDefault("tags", []);
            var permissions = lists[kind == RegistryKind.Skill ? "required_permissions" : "permissions"];
            var tools = kind == RegistryKind.Skill ? lists["required_tools"] : [];
            var capabilities = kind == RegistryKind.Tool ? lists["capabilities"] : [];
            Require(tags.All(t => Identifier.IsMatch(t)) && tools.All(t => Identifier.IsMatch(t) && !IsDeviceName(t)), "Invalid tag or tool id.");
            Require(permissions.All(p => p.Length <= 100 && PermissionName.IsMatch(p)), "Invalid permission name.");
            Require(capabilities.All(c => CapabilityName.IsMatch(c)), "Invalid capability name.");
            var runtimeRequirements = kind == RegistryKind.Skill ? lists.GetValueOrDefault("runtime_requirements", []) : [];
            Require(runtimeRequirements.All(RuntimeRequirement.IsMatch), "Invalid runtime requirement.");
            var provider = kind == RegistryKind.Tool ? text["provider"] : null;
            Require(provider is null or "mcp" or "http", "Tool provider must be mcp or http.");
            return new(kind, directoryId, text["name"], text["description"], tags, permissions, tools, provider, capabilities,
                $"{(kind == RegistryKind.Skill ? "skills" : "tools")}/{directoryId}/manifest.yaml", contentHash, instructions,
                runtimeRequirements);
        }
        catch (YamlException) { throw new BrainException(422, "Invalid manifest YAML; use a single mapping with text and text-list fields, without aliases or tags."); }
    }

    private static string Scalar(IParser parser, int maximum)
    {
        var value = parser.Consume<Scalar>();
        CheckNode(value);
        Require(!string.IsNullOrWhiteSpace(value.Value) && value.Value.Length <= maximum &&
            !value.Value.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'), "Invalid manifest text.");
        return value.Value.Trim();
    }

    private static void CheckNode(NodeEvent node) => Require(node.Anchor.IsEmpty && node.Tag.IsEmpty, "YAML anchors and tags are not supported.");
    private static void Require(bool condition, string message) { if (!condition) throw new BrainException(422, message); }
    private static bool IsDeviceName(string id) => Regex.IsMatch(id, @"\A(con|prn|aux|nul|com[0-9]|lpt[0-9])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(value))).ToLowerInvariant();

    private static void CheckPath(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new BrainException(422, "Registry links are prohibited.");
    }
    private static void CheckAncestors(string path)
    {
        for (var part = new DirectoryInfo(path); part is not null; part = part.Parent) CheckPath(part.FullName);
    }
    private static async Task<string> ReadFileAsync(string path, int maximum, CancellationToken ct)
    {
        CheckPath(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        Require(stream.Length <= maximum, "Registry file is too large.");
        var bytes = new byte[maximum + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        Require(count <= maximum, "Registry file is too large.");
        CheckPath(path);
        return Utf8.GetString(bytes, 0, count).TrimStart('\uFEFF');
    }
}
