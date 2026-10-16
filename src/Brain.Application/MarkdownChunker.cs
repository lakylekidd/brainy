using System.Text.RegularExpressions;

namespace Brain.Application;

public sealed partial class MarkdownChunker : IMarkdownChunker
{
    public string Version => "markdown-sections-v1-1800";
    public const int MaximumChunkCharacters = 1800;

    public ChunkedMarkdown Chunk(string path, string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var first = 0;
        if (lines[0].Trim() == "---")
        {
            var end = Array.FindIndex(lines, 1, l => l.Trim() is "---" or "...");
            if (end >= 0) first = end + 1;
        }
        string? title = null;
        var headings = new List<(int Level, string Title)>();
        var chunks = new List<MarkdownChunk>();
        var buffer = new System.Text.StringBuilder();
        var startLine = first + 1;
        var endLine = startLine;
        var heading = "";
        char fence = '\0';
        var fenceLength = 0;
        void Flush()
        {
            if (!string.IsNullOrWhiteSpace(buffer.ToString()))
                chunks.Add(new(chunks.Count, heading, buffer.ToString(), startLine, endLine));
            buffer.Clear();
        }
        for (var i = first; i < lines.Length; i++)
        {
            var line = lines[i];
            var fenceMatch = Fence().Match(line);
            var isHeading = fence == '\0' && !fenceMatch.Success ? Heading().Match(line) : Match.Empty;
            if (isHeading.Success)
            {
                Flush();
                var level = isHeading.Groups[1].Length;
                var text = Regex.Replace(isHeading.Groups[2].Value, @"\s+#+\s*$", "").Trim();
                if (text.Length > 200) text = text[..(char.IsHighSurrogate(text[199]) ? 199 : 200)];
                if (level == 1 && title is null) title = text;
                headings.RemoveAll(h => h.Level >= level);
                headings.Add((level, text));
                heading = string.Join(" / ", headings.Select(h => h.Title));
            }
            if (fenceMatch.Success)
            {
                var marker = fenceMatch.Groups[1].Value;
                if (fence == '\0') { fence = marker[0]; fenceLength = marker.Length; }
                else if (marker[0] == fence && marker.Length >= fenceLength && string.IsNullOrWhiteSpace(fenceMatch.Groups[2].Value)) fence = '\0';
            }
            var remainder = line;
            do
            {
                if (buffer.Length > 0 && buffer.Length + 1 + remainder.Length > MaximumChunkCharacters) Flush();
                if (buffer.Length == 0) startLine = i + 1;
                else buffer.Append('\n');
                var take = Math.Min(MaximumChunkCharacters - buffer.Length, remainder.Length);
                if (take > 0 && take < remainder.Length && char.IsHighSurrogate(remainder[take - 1])) take--;
                buffer.Append(remainder.AsSpan(0, take));
                endLine = i + 1;
                remainder = remainder[take..];
                if (remainder.Length > 0) Flush();
            } while (remainder.Length > 0);
        }
        Flush();
        title ??= System.IO.Path.GetFileNameWithoutExtension(path).Replace('-', ' ').Replace('_', ' ');
        return new(title, chunks);
    }
    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.+)$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})(.*)$")]
    private static partial Regex Fence();
}
