using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;

namespace Brain.UnitTests;

public sealed class GraphTests
{
    [Fact]
    public void References_are_explicit_bounded_deduplicated_and_do_not_extract_arbitrary_metadata_or_body()
    {
        var parser = new GraphReferenceExtractor();
        Assert.Equal(["document:Infrastructure/cluster.md", "skill:triage"], parser.Extract(
            "\uFEFF---\r\ntitle: graph_references\r\nnested:\r\n  graph_references: [tool:hidden]\r\n" +
            "graph_references: [document:Infrastructure/cluster.md, skill:triage, skill:triage]\r\n---\r\n# Body\r\ntool:ignored"));
        Assert.Empty(parser.Extract("# Body\ngraph_references: [tool:ignored]"));
        Assert.Empty(parser.Extract("---\ntitle: graph_references\n---\nBody"));
        Assert.Empty(parser.Extract("---\ntitle: open frontmatter"));
        Assert.Empty(parser.Extract("---\nunrelated: [invalid YAML\n---\n# Still indexable"));
        Assert.Equal(["tool:logs"], parser.Extract("---\n'graph_references' : [tool:logs]\n---"));
    }

    [Theory]
    [InlineData("graph_references: tool:triage")]
    [InlineData("graph_references: [unknown:triage]")]
    [InlineData("graph_references: [document:../outside.md]")]
    [InlineData("graph_references: [{id: tool:triage}]")]
    [InlineData("graph_references: [tool:triage]\ngraph_references: [tool:other]")]
    [InlineData("graph_references: &refs [tool:triage]")]
    [InlineData("refs: &refs [tool:triage]\ngraph_references: *refs")]
    public void Invalid_or_ambiguous_references_fail_explicitly(string yaml) =>
        Assert.Equal(422, Assert.Throws<BrainException>(() => new GraphReferenceExtractor().Extract($"---\n{yaml}\n---\nBody")).StatusCode);

    [Fact]
    public void Reference_counts_and_relationship_identity_are_bounded_and_stable()
    {
        Assert.Equal(422, Assert.Throws<BrainException>(() => new GraphReferenceExtractor().Extract(
            "---\ngraph_references: [" + string.Join(", ", Enumerable.Range(0, 101).Select(i => "tool:tool-" + i)) + "]\n---")).StatusCode);
        var first = GraphRelationship.Create("requires_tool", "skill:triage", "tool:logs");
        Assert.Equal(first, GraphRelationship.Create("requires_tool", "skill:triage", "tool:logs"));
        Assert.NotEqual(first.Id, GraphRelationship.Create("references", "skill:triage", "tool:logs").Id);
        Assert.NotEqual(first.Id, GraphRelationship.Create("requires_tool", "tool:logs", "skill:triage").Id);
    }

    [Fact]
    public async Task Traversal_never_exceeds_the_total_node_budget_or_fetches_the_full_large_graph()
    {
        var source = new LargeSource();
        var service = new GraphService(source);
        var graph = await service.NeighborsAsync(Actor, "document:root.md", 2, 10);
        Assert.Equal(10, graph.Entities.Count);
        Assert.True(graph.Truncated);
        Assert.Equal(1, source.NeighborCalls);
        Assert.All(graph.Relationships, edge =>
        {
            Assert.Contains(graph.Entities, node => node.Id == edge.Source);
            Assert.Contains(graph.Entities, node => node.Id == edge.Target);
        });
        source.NeighborCalls = 0;
        var entityOnly = await service.NeighborsAsync(Actor, "document:root.md", 0, 10);
        Assert.Single(entityOnly.Entities);
        Assert.Equal(0, source.NeighborCalls);
    }

    [Fact]
    public async Task Graph_operations_require_a_source_grant_and_validate_before_fetching()
    {
        var source = new LargeSource();
        var service = new GraphService(source);
        var denied = new AgentContext("none", null, new HashSet<string>());
        Assert.Equal(403, (await Assert.ThrowsAsync<BrainException>(() => service.SearchAsync(denied, new()))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => service.SearchAsync(Actor, new(Types: ["unknown"])))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => service.SearchAsync(Actor, new(Limit: 201)))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => service.NeighborsAsync(Actor, "document:root.md", 3))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<BrainException>(() => service.GetAsync(Actor, "document:../outside.md"))).StatusCode);
        Assert.Equal(0, source.NeighborCalls);
    }

    private static readonly AgentContext Actor = new("reader", null, new HashSet<string> { Permissions.KnowledgeRead });
    private static GraphEntity Node(string id) => new(id, "document", id, new("knowledge_document", id["document:".Length..]), new Dictionary<string, string>());
    private sealed class LargeSource : IGraphSource
    {
        public int NeighborCalls { get; set; }
        public Task<GraphEntityPage> SearchAsync(AgentContext actor, GraphQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<GraphEntity?> FindAsync(AgentContext actor, string id, CancellationToken ct) => Task.FromResult<GraphEntity?>(Node(id));
        public Task<GraphResult> NeighborsAsync(AgentContext actor, string id, string[]? types, int limit, CancellationToken ct)
        {
            NeighborCalls++;
            return Task.FromResult(new GraphResult(Enumerable.Range(0, limit).Select(i => Node($"document:neighbor-{i}.md")).ToArray(), [], true));
        }
        public Task<IReadOnlyList<GraphRelationship>> RelationshipsAsync(AgentContext actor, IReadOnlyList<GraphEntity> entities, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GraphRelationship>>(entities.Skip(1).Select(node =>
                GraphRelationship.Create("references", entities[0].Id, node.Id)).ToArray());
    }
}
