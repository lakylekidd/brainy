import { Credentials, request } from "../../api/client";

export const entityTypes = ["document", "skill", "tool", "agent", "role", "permission"] as const;
export type EntityType = typeof entityTypes[number];
export interface GraphEntity {
  id: string;
  type: EntityType;
  label: string;
  source: { kind: string; id: string };
  metadata: { path?: string; indexedAt?: string; enabled?: string; provider?: string };
}
export interface GraphRelationship {
  id: string;
  type: string;
  source: string;
  target: string;
}
export interface GraphResult {
  entities: GraphEntity[];
  relationships: GraphRelationship[];
  truncated: boolean;
  depth: number;
}
export const emptyGraph: GraphResult = { entities: [], relationships: [], truncated: false, depth: 0 };
export const maxEntities = 200;
export const maxRelationships = 400;

export function loadGraph(credentials: Credentials, types: EntityType[], query: string, signal: AbortSignal) {
  const parameters = new URLSearchParams({ types: types.join(","), limit: "80" });
  if (query) parameters.set("query", query);
  return request<GraphResult>(`/api/graph${query ? "/search" : ""}?${parameters}`, credentials, signal);
}
export function loadEntity(credentials: Credentials, id: string, signal: AbortSignal) {
  return request<GraphEntity>(`/api/graph/entity?${new URLSearchParams({ id })}`, credentials, signal);
}
export function loadNeighbors(credentials: Credentials, id: string, types: EntityType[], depth: number, signal: AbortSignal) {
  return request<GraphResult>(`/api/graph/neighbors?${new URLSearchParams({
    id, types: types.join(","), depth: String(depth), limit: "80",
  })}`, credentials, signal);
}
export function mergeGraph(current: GraphResult, incoming: GraphResult): GraphResult {
  const nodes = new Map(current.entities.map((node) => [node.id, node]));
  for (const node of incoming.entities) {
    if (nodes.has(node.id) || nodes.size < maxEntities) nodes.set(node.id, node);
  }
  const edges = new Map(current.relationships.map((edge) => [edge.id, edge]));
  for (const edge of incoming.relationships) {
    if (nodes.has(edge.source) && nodes.has(edge.target) && (edges.has(edge.id) || edges.size < maxRelationships))
      edges.set(edge.id, edge);
  }
  return {
    entities: [...nodes.values()],
    relationships: [...edges.values()],
    depth: Math.max(current.depth, incoming.depth),
    truncated: current.truncated || incoming.truncated ||
      incoming.entities.some((node) => !nodes.has(node.id)) ||
      incoming.relationships.some((edge) => !edges.has(edge.id)),
  };
}
