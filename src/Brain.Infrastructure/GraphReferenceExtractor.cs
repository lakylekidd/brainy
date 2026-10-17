using System.Text.RegularExpressions;
using Brain.Application;
using Brain.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Brain.Infrastructure;

public sealed class GraphReferenceExtractor : IGraphReferenceExtractor
{
    private static readonly Regex ReferenceField = new("^(?:graph_references|'graph_references'|\"graph_references\")\\s*:",
        RegexOptions.CultureInvariant);

    public string[] Extract(string markdown)
    {
        using var reader = new StringReader(markdown.TrimStart('\uFEFF'));
        if (reader.ReadLine() != "---") return [];
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null && line != "---")
        {
            lines.Add(line);
        }
        if (line is null) return [];
        if (!lines.Any(ReferenceField.IsMatch)) return [];
        if (lines.Count > 2000) throw new BrainException(422, "Graph frontmatter exceeds 2000 lines.");
        try
        {
            var parser = new Parser(new StringReader(string.Join('\n', lines)));
            var events = new List<ParsingEvent>();
            while (parser.MoveNext())
            {
                if (events.Count >= 10000) throw new BrainException(422, "Graph frontmatter is too complex.");
                events.Add(parser.Current!);
            }
            // Ignore unrelated YAML fields. Only a top-level scalar list is a graph contract.
            var seen = false;
            var references = new List<string>();
            var i = 0;
            while (i < events.Count && events[i] is StreamStart or DocumentStart) i++;
            if (i == events.Count || events[i++] is not MappingStart) return [];
            while (i < events.Count && events[i] is not MappingEnd)
            {
                var graphKey = events[i] is Scalar { Value: "graph_references" };
                SkipNode(events, ref i);
                if (!graphKey) { SkipNode(events, ref i); continue; }
                if (seen || i >= events.Count || events[i++] is not SequenceStart { Anchor.IsEmpty: true, Tag.IsEmpty: true })
                    throw new BrainException(422, "graph_references must be one list of graph entity ids.");
                seen = true;
                while (i < events.Count && events[i] is not SequenceEnd)
                {
                    if (events[i] is not Scalar value || !value.Anchor.IsEmpty || !value.Tag.IsEmpty ||
                        !GraphIds.IsValid(value.Value) || references.Count >= 100)
                        throw new BrainException(422, "graph_references must contain at most 100 valid graph entity ids.");
                    references.Add(value.Value);
                    i++;
                }
                i++;
            }
            return references.Distinct(StringComparer.Ordinal).ToArray();
        }
        catch (YamlException) { throw new BrainException(422, "Invalid graph frontmatter YAML."); }
    }

    private static void SkipNode(IReadOnlyList<ParsingEvent> events, ref int index)
    {
        if (index >= events.Count) return;
        var depth = events[index++] is MappingStart or SequenceStart ? 1 : 0;
        while (depth > 0 && index < events.Count)
        {
            var node = events[index++];
            if (node is MappingStart or SequenceStart) depth++;
            if (node is MappingEnd or SequenceEnd) depth--;
        }
    }
}
