import { MultiDirectedGraph } from "graphology";
import { entityTypes, GraphResult } from "./api";

export const typeColors = {
  document: "#5577d8", skill: "#18a878", tool: "#d08730",
  agent: "#8a70d6", role: "#c55f93", permission: "#638694",
};

export function createGraph(data: GraphResult) {
  const graph = new MultiDirectedGraph();
  const counts = new Map<string, number>();
  for (const node of data.entities) {
    const index = counts.get(node.type) ?? 0;
    counts.set(node.type, index + 1);
    const angle = entityTypes.indexOf(node.type) * Math.PI / 3;
    const offset = index * 2.399963229728653;
    const radius = Math.sqrt(index) * 0.13;
    graph.addNode(node.id, {
      label: node.label, color: typeColors[node.type], size: 7,
      x: Math.cos(angle) * 2 + Math.cos(offset) * radius,
      y: Math.sin(angle) * 2 + Math.sin(offset) * radius,
    });
  }
  for (const edge of data.relationships) {
    if (!graph.hasNode(edge.source) || !graph.hasNode(edge.target) || graph.hasEdge(edge.id)) continue;
    graph.addDirectedEdgeWithKey(edge.id, edge.source, edge.target, {
      label: edge.type.replaceAll("_", " "), size: 1.5, type: "arrow",
    });
  }
  return graph;
}
