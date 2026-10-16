using System.Text.RegularExpressions;
using Brain.Domain;

namespace Brain.Application;

public static partial class KnowledgeChangePlanner
{
    public static string Plan(KnowledgeOperation operation, string content, string? section, VaultDocument? before, string? revisionToken,
        IMetadataDocumentProcessor? metadata = null)
    {
        if (operation == KnowledgeOperation.CreateDocument)
        {
            if (before is not null) throw new BrainException(409, "The document already exists.");
            Validation.Require(revisionToken is null, "New documents require an absent revision token.");
            return content;
        }
        if (before is null) throw new BrainException(409, "The document no longer exists.");
        if (before.RevisionToken != revisionToken) throw new BrainException(409, "The document revision has changed; read it again and submit a new proposal.");
        if (operation == KnowledgeOperation.MetadataEnrichment)
            return (metadata ?? throw new InvalidOperationException("A metadata processor is required.")).Apply(before, content);
        var newline = before.Content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (operation == KnowledgeOperation.Append)
            return before.Content + (before.Content.Length == 0 || before.Content.EndsWith('\n') ? "" : newline) + content;
        Validation.Require(operation == KnowledgeOperation.ReplaceSection, "Unsupported knowledge operation.");
        var headings = Headings(before.Content);
        var matches = headings.Where(h => h.Path == section).ToArray();
        Validation.Require(matches.Length == 1, "Section must identify exactly one ATX heading hierarchy.");
        var selected = matches[0];
        var end = headings.FirstOrDefault(h => h.Start > selected.Start && h.Level <= selected.Level)?.Start ?? before.Content.Length;
        var body = content.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline);
        var headingTerminator = selected.BodyStart > 0 && before.Content[selected.BodyStart - 1] == '\n' ? "" : newline;
        return before.Content[..selected.BodyStart] + headingTerminator + body + (body.EndsWith('\n') ? "" : newline) + before.Content[end..];
    }

    private sealed record HeadingSpan(string Path, int Level, int Start, int BodyStart);
    private static List<HeadingSpan> Headings(string content)
    {
        var lines = Lines().Matches(content).Where(m => m.Length > 0).ToArray();
        var first = 0;
        if (lines.Length > 0 && lines[0].Value.Trim() == "---")
        {
            var end = Array.FindIndex(lines, 1, l => l.Value.Trim() is "---" or "...");
            if (end >= 0) first = end + 1;
        }
        var result = new List<HeadingSpan>();
        var hierarchy = new List<(int Level, string Title)>();
        char fence = '\0';
        var fenceLength = 0;
        foreach (var line in lines.Skip(first))
        {
            var text = line.Value.TrimEnd('\r', '\n');
            var marker = Fence().Match(text);
            if (marker.Success)
            {
                var run = marker.Groups[1].Value;
                if (fence == '\0') { fence = run[0]; fenceLength = run.Length; }
                else if (run[0] == fence && run.Length >= fenceLength && string.IsNullOrWhiteSpace(marker.Groups[2].Value)) fence = '\0';
                continue;
            }
            if (fence != '\0') continue;
            var heading = Heading().Match(text);
            if (!heading.Success) continue;
            var level = heading.Groups[1].Length;
            var title = Regex.Replace(heading.Groups[2].Value, @"\s+#+\s*$", "").Trim();
            hierarchy.RemoveAll(h => h.Level >= level);
            hierarchy.Add((level, title));
            result.Add(new(string.Join(" / ", hierarchy.Select(h => h.Title)), level, line.Index, line.Index + line.Length));
        }
        return result;
    }

    [GeneratedRegex(@"[^\n]*(?:\n|$)")]
    private static partial Regex Lines();
    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.+)$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})(.*)$")]
    private static partial Regex Fence();
}
