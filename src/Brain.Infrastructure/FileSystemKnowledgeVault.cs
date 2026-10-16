using System.Security.Cryptography;
using System.Text;
using Brain.Application;
using Brain.Domain;

namespace Brain.Infrastructure;

public sealed class KnowledgeOptions
{
    public string? VaultPath { get; set; }
    public int MaximumDocumentBytes { get; set; } = 2 * 1024 * 1024;
    public bool WriterEnabled { get; set; }
}

/// <summary>Read-only canonical Markdown access; intentionally has no write operations.</summary>
public sealed class FileSystemKnowledgeVault(KnowledgeOptions options) : IKnowledgeVault
{
    public IReadOnlyList<string> ListMarkdownPaths(CancellationToken ct)
    {
        var root = Root();
        var paths = new List<string>();
        var directories = new Stack<string>();
        directories.Push(root);
        try
        {
            while (directories.TryPop(out var directory))
            {
                ct.ThrowIfCancellationRequested();
                EnsureNotLinked(directory);
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    if (Path.GetFileName(entry).StartsWith('.')) continue;
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) directories.Push(entry);
                    else if (Path.GetExtension(entry).Equals(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        var relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/');
                        try { ValidateRelativePath(relative); paths.Add(relative); }
                        catch (BrainException) { /* Nonportable filenames are excluded consistently with read access. */ }
                    }
                }
            }
        }
        catch (IOException) { throw new BrainException(503, "Vault scan could not complete; index pruning was not performed."); }
        catch (UnauthorizedAccessException) { throw new BrainException(503, "Vault scan could not complete; index pruning was not performed."); }
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    public async Task<VaultDocument> ReadAsync(string path, CancellationToken ct)
    {
        ValidateRelativePath(path);
        var root = Root();
        var fullPath = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, comparison))
            throw new BrainException(400, "Path must stay inside the vault.");
        try
        {
            CheckComponents(root, path);
            var info = new FileInfo(fullPath);
            if (!info.Exists) throw new BrainException(404, "Document not found.");
            if (info.Length > options.MaximumDocumentBytes) throw new BrainException(413, "Markdown document exceeds the configured size limit.");
            var modified = info.LastWriteTimeUtc;
            var length = info.Length;
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > options.MaximumDocumentBytes) throw new BrainException(413, "Markdown document exceeds the configured size limit.");
                bytes.Write(buffer, 0, count);
            }
            CheckComponents(root, path);
            info.Refresh();
            if (!info.Exists) throw new BrainException(404, "Document not found.");
            if (info.LastWriteTimeUtc != modified || info.Length != length || bytes.Length != length)
                throw new BrainException(422, "Document changed while being read; retry the read.");
            var data = bytes.ToArray();
            var contentHash = Convert.ToHexStringLower(SHA256.HashData(data));
            var content = new UTF8Encoding(false, true).GetString(data);
            var bom = content.StartsWith('\uFEFF');
            if (bom) content = content[1..];
            return new(path, content, contentHash, RevisionTokens.Create(path, contentHash), new DateTimeOffset(modified), bom);
        }
        catch (FileNotFoundException) { throw new BrainException(404, "Document not found."); }
        catch (DirectoryNotFoundException) { throw new BrainException(404, "Document not found."); }
        catch (DecoderFallbackException) { throw new BrainException(422, "Markdown documents must use UTF-8 encoding."); }
        catch (UnauthorizedAccessException) { throw new BrainException(404, "Document not found."); }
        catch (IOException) { throw new BrainException(422, "Document could not be read consistently; retry the read."); }
    }

    internal string Root()
    {
        if (string.IsNullOrWhiteSpace(options.VaultPath)) throw new BrainException(503, "Knowledge vault is not configured.");
        if (options.MaximumDocumentBytes is < 1 or > 64 * 1024 * 1024)
            throw new InvalidOperationException("MaximumDocumentBytes must be between 1 and 67108864.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.VaultPath));
        if (!Directory.Exists(root)) throw new BrainException(503, "Configured knowledge vault is unavailable.");
        EnsureNotLinked(root);
        return root;
    }

    internal string ResolveForWrite(string path)
    {
        ValidateRelativePath(path);
        var root = Root();
        CheckComponents(root, path, allowMissing: true);
        return Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void CheckComponents(string root, string path, bool allowMissing = false)
    {
        EnsureNotLinked(root);
        var current = root;
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var parent = current;
            current = Path.Combine(current, segments[i]);
            try { EnsureNotLinked(current); }
            catch (FileNotFoundException) when (allowMissing) { break; }
            catch (DirectoryNotFoundException) when (allowMissing) { break; }
            // Reject case aliases on Windows so revision paths and Git paths have one spelling.
            if (OperatingSystem.IsWindows() && !Directory.EnumerateFileSystemEntries(parent)
                .Any(p => Path.GetFileName(p).Equals(segments[i], StringComparison.Ordinal)))
                throw new BrainException(400, "Knowledge paths must use the exact filename casing.");
            if (i < segments.Length - 1 && !Directory.Exists(current)) throw new BrainException(400, "A knowledge parent path is not a directory.");
        }
    }
    private static void EnsureNotLinked(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new BrainException(400, "Linked files and directories are not allowed in knowledge paths.");
    }
    private static void ValidateRelativePath(string? path)
    {
        Validation.Require(!string.IsNullOrWhiteSpace(path) && path.Length <= 1000 && !Path.IsPathRooted(path) &&
            !path.Any(c => char.IsControl(c) || "\\:<>\"|?*".Contains(c)), "Invalid vault-relative Markdown path.");
        Validation.Require(path!.Split('/').All(s => s.Length > 0 && Encoding.UTF8.GetByteCount(s) <= 255 &&
            s is not "." and not ".." && !s.StartsWith('.') && !s.EndsWith('.') && !s.EndsWith(' ') && !IsDeviceName(s)),
            "Invalid vault-relative Markdown path.");
        Validation.Require(Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase), "Only Markdown documents may be read.");
    }
    private static bool IsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
             "123456789¹²³".Contains(stem[3]));
    }
}

public static class RevisionTokens
{
    // Content-addressed version identifier, not an authentication credential or approval to write.
    public static string Create(string path, string contentHash) => "v1." + Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(path + "\0" + contentHash)));
    public static bool Matches(string token, string path, string contentHash) =>
        string.Equals(token, Create(path, contentHash), StringComparison.Ordinal);
}
