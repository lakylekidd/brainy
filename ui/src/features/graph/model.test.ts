import { describe, expect, it } from "vitest";
import { GraphEntity, GraphResult, maxEntities, maxRelationships, mergeGraph } from "./api";
import { createGraph, typeColors } from "./model";

function node(id: string): GraphEntity {
  return { id, type: "document", label: id, source: { kind: "knowledge_document", id }, metadata: {} };
}
const result = (entities: GraphEntity[], relationships: GraphResult["relationships"] = []): GraphResult =>
  ({ entities, relationships, truncated: false, depth: 0 });

describe("graph rendering model", () => {
  it("renders typed stable nodes and directed relationships, without dangling or duplicate edges", () => {
    const edge = { id: "edge-1", type: "requires_tool", source: "document:a", target: "document:b" };
    const graph = createGraph(result([node("document:a"), node("document:b")], [
      edge, edge, { ...edge, id: "edge-dangling", target: "document:missing" },
    ]));
    expect(graph.order).toBe(2);
    expect(graph.size).toBe(1);
    expect(graph.source("edge-1")).toBe("document:a");
    expect(graph.target("edge-1")).toBe("document:b");
    expect(graph.getNodeAttribute("document:a", "color")).toBe(typeColors.document);
    expect(graph.getNodeAttribute("document:a", "label")).toBe("document:a");
    expect(Number.isFinite(graph.getNodeAttribute("document:a", "x"))).toBe(true);
    expect(graph.getEdgeAttribute("edge-1", "label")).toBe("requires tool");
    expect(graph.getEdgeAttribute("edge-1", "type")).toBe("arrow");
  });

  it("bounds progressive loading and keeps only edges between loaded entities", () => {
    const incoming = result(Array.from({ length: 1000 }, (_, index) => node(`document:${index}`)),
      Array.from({ length: 1000 }, (_, index) => ({
        id: `edge:${index}`, type: "references", source: "document:root", target: `document:${index}`,
      })));
    const graph = mergeGraph(result([node("document:root")]), incoming);
    expect(graph.entities).toHaveLength(maxEntities);
    expect(graph.relationships.length).toBeLessThanOrEqual(maxRelationships);
    expect(graph.truncated).toBe(true);
    const ids = new Set(graph.entities.map((entity) => entity.id));
    for (const edge of graph.relationships) expect(ids.has(edge.source) && ids.has(edge.target)).toBe(true);
    expect(mergeGraph(graph, incoming)).toEqual(graph);
  });

  it("caps dense relationship payloads independently of the node budget", () => {
    const graph = mergeGraph(result([]), result([node("document:a"), node("document:b")],
      Array.from({ length: 500 }, (_, index) => ({
        id: `edge:${index}`, type: "references", source: "document:a", target: "document:b",
      }))));
    expect(graph.relationships).toHaveLength(maxRelationships);
    expect(graph.truncated).toBe(true);
  });
});
