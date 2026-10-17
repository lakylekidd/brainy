import {
  forceCenter, forceCollide, forceLink, forceManyBody, forceSimulation,
  hierarchy, pack, scaleUtc, utcDay,
} from "d3";
import type { SimulationNodeDatum } from "d3";
import { GROUPS, ROOT_FILE, type VaultFile, type VaultGraph } from "./vault";

export const VIEWS = ["Ring", "Circle", "Areas", "Links", "Timeline", "3D Orbit"] as const;
export type ViewMode = typeof VIEWS[number];
export const RINGS = [
  { id: "skills", label: "Skills", fraction: 0.27, color: "#f3a45f" },
  { id: "knowledge", label: "Knowledge", fraction: 0.52, color: "#94a7c7" },
  { id: "memory", label: "Memory", fraction: 0.71, color: "#bda4ed" },
  { id: "routines", label: "Routines", fraction: 0.86, color: "#e8c67e" },
  { id: "applications", label: "Applications", fraction: 1, color: "#9cbfe6" },
] as const;
export type RingId = typeof RINGS[number]["id"];
export function ringId(file: VaultFile): RingId {
  return RINGS.find((ring) => ring.id === file.group)?.id ?? "knowledge";
}
export type Placement = { kind: "angle"; angle: number } | { kind: "offset"; x: number; y: number };
export interface PlotNode extends SimulationNodeDatum {
  id: string;
  file: VaultFile;
  x: number;
  y: number;
  radius: number;
  depth: number;
  ring?: { rx: number; ry: number; angle: number };
}
export interface Guide {
  kind: "ellipse" | "line" | "label" | "path";
  x: number; y: number;
  rx: number; ry: number;
  label?: string;
  color?: string;
  points?: { x: number; y: number }[];
}
export interface Layout { nodes: PlotNode[]; guides: Guide[] }
const TAU = Math.PI * 2;

export function createLayout(graph: VaultGraph, view: ViewMode, width: number, height: number, rotation = 0): Layout {
  const cx = width / 2;
  const cy = height / 2;
  const radius = Math.max(50, Math.min(width / 2 - (width < 500 ? 24 : 55), height / 2 - 62));
  const ringHeight = view === "Ring" ? Math.max(radius, Math.min(height / 2 - 62, radius * 1.9)) : radius;
  const groups = GROUPS.filter((group) => graph.files.some((file) => file.group === group.id));
  const nodes: PlotNode[] = graph.files.map((file) => ({
    id: file.id, file, x: cx, y: cy, radius: file.id === ROOT_FILE ? 19 : 5.5, depth: 1,
  }));
  const guides: Guide[] = [];
  const satellites = nodes.filter((node) => node.id !== ROOT_FILE);
  RINGS.forEach((ring, index) => {
    const members = satellites.filter((node) => ringId(node.file) === ring.id);
    const r = radius * ring.fraction;
    const ry = ringHeight * ring.fraction;
    members.forEach((node, i) => {
      const area = GROUPS.findIndex((group) => group.id === node.file.group);
      const peers = members.filter((item) => item.file.group === node.file.group);
      const angle = ring.id === "knowledge"
        ? -Math.PI / 2 + (area + 0.5) * TAU / 5 + (peers.indexOf(node) - (peers.length - 1) / 2) * 0.23
        : (i + 0.5) / members.length * TAU - Math.PI / 2 + index * 0.13;
      node.x = cx + Math.cos(angle) * r;
      node.y = cy + Math.sin(angle) * ry;
      if (view === "Ring") node.ring = { rx: r, ry, angle };
    });
    if (view === "Ring" && members.length) guides.push({
      kind: "ellipse", x: cx, y: cy, rx: r, ry,
      label: `${ring.label} / ${members.length}`, color: ring.color,
    });
  });
  if (view === "Ring") {
    GROUPS.slice(0, 5).forEach((group, index) => {
      const count = satellites.filter((node) => node.file.group === group.id).length;
      if (!count) return;
      const angle = -Math.PI / 2 + (index + 0.5) * TAU / 5;
      guides.push({
        kind: "label", x: cx + Math.cos(angle) * radius * 0.39, y: cy + Math.sin(angle) * ringHeight * 0.39,
        rx: 0, ry: 0, label: `${group.label} ${count}`, color: group.color,
      });
    });
  }

  if (view === "Circle") {
    satellites.forEach((node, index) => {
      const angle = index / Math.max(1, satellites.length) * TAU - Math.PI / 2;
      node.x = cx + Math.cos(angle) * radius;
      node.y = cy + Math.sin(angle) * radius;
    });
    guides.push({ kind: "ellipse", x: cx, y: cy, rx: radius, ry: radius });
  }

  if (view === "Areas" && nodes.length) {
    interface Cluster { name: string; children?: Cluster[]; value?: number }
    const tree = hierarchy<Cluster>({
      name: "vault",
      children: [
        ...groups.map((group) => ({
          name: group.id, children: nodes.filter((node) => node.file.group === group.id).map((node) => ({ name: node.id, value: 1 })),
        })),
        { name: ROOT_FILE, value: 1.5 },
      ],
    }).sum((item) => item.value ?? 0).sort((a, b) => (b.value ?? 0) - (a.value ?? 0));
    const size = Math.min(width - 36, height - 60);
    const packed = pack<Cluster>().size([size, size]).padding(23)(tree);
    for (const leaf of packed.leaves()) {
      const node = nodes.find((item) => item.id === leaf.data.name);
      if (node) {
        node.x = cx + leaf.x - size / 2;
        node.y = cy + leaf.y - size / 2;
      }
    }
    packed.children?.filter((child) => child.children).forEach((child) => {
      const group = groups.find((item) => item.id === child.data.name)!;
      guides.push({
        kind: "ellipse", x: cx + child.x - size / 2, y: cy + child.y - size / 2,
        rx: child.r, ry: child.r, label: group.label, color: group.color,
      });
    });
  }

  if (view === "Links" && nodes.length) {
    const root = nodes.find((node) => node.id === ROOT_FILE);
    if (root) { root.fx = cx; root.fy = cy; }
    // Stop the simulation before ticking so no timer or mutable D3 state escapes the layout.
    const simulation = forceSimulation(nodes).stop()
      .force("links", forceLink<PlotNode, { source: string; target: string }>(graph.links.map((link) => ({ ...link })))
        .id((node) => node.id).distance(94).strength(0.32))
      .force("charge", forceManyBody().strength(-220))
      .force("collide", forceCollide<PlotNode>((node) => node.radius + 16))
      .force("center", forceCenter(cx, cy));
    simulation.tick(240);
    const spread = Math.max(1, ...nodes.map((node) => Math.max(Math.abs(node.x - cx), Math.abs(node.y - cy))));
    const fit = Math.min(1, radius / spread);
    nodes.forEach((node) => { node.x = cx + (node.x - cx) * fit; node.y = cy + (node.y - cy) * fit; });
  }

  if (view === "Timeline") {
    const dates = graph.files.map((file) => new Date(`${file.created}T00:00:00Z`));
    const first = new Date(Math.min(...dates.map(Number)));
    const last = new Date(Math.max(...dates.map(Number)));
    const scale = scaleUtc().domain([utcDay.offset(first, -1), utcDay.offset(last, 1)]).range([90, width - 65]);
    const lanes = ["brain", ...groups.map((group) => group.id)];
    const step = (height - 115) / Math.max(1, lanes.length - 1);
    lanes.forEach((group, index) => {
      const y = 44 + index * step;
      guides.push({ kind: "line", x: 90, y, rx: width - 155, ry: 0 });
      guides.push({ kind: "label", x: 8, y: y - 12, rx: 0, ry: 0, label: group === "brain" ? "Brain" : groups.find((item) => item.id === group)?.label });
    });
    nodes.forEach((node) => {
      const peers = nodes.filter((item) => item.file.group === node.file.group && item.file.created === node.file.created);
      const spacing = Math.min(16, step * 0.6 / Math.max(1, peers.length - 1));
      node.x = scale(new Date(`${node.file.created}T00:00:00Z`));
      node.y = 44 + lanes.indexOf(node.file.group) * step + (peers.indexOf(node) - (peers.length - 1) / 2) * spacing;
    });
    scale.ticks(Math.max(2, Math.floor(width / 160))).forEach((tick) => guides.push({
      kind: "label", x: scale(tick), y: height - 27, rx: 0, ry: 0,
      label: tick.toISOString().slice(5, 10).replace("-", "/"),
    }));
  }

  if (view === "3D Orbit") {
    const project = (x: number, y: number, z: number) => {
      const rotatedX = x * Math.cos(rotation) - z * Math.sin(rotation);
      const rotatedZ = z * Math.cos(rotation) + x * Math.sin(rotation);
      const perspective = 2.8 / (2.8 - rotatedZ);
      return { x: cx + rotatedX * radius * 0.78 * perspective, y: cy + (y * 0.8 + rotatedZ * 0.2) * radius * perspective };
    };
    RINGS.forEach((ring, index) => {
      const tilt = 0.35 + index * 0.23;
      const points = Array.from({ length: 81 }, (_, i) => {
        const angle = i / 80 * TAU;
        return project(Math.cos(angle), Math.sin(angle) * Math.cos(tilt), Math.sin(angle) * Math.sin(tilt));
      });
      guides.push({
        kind: "path", x: cx, y: cy, rx: 0, ry: 0, color: ring.color, points,
      });
    });
    satellites.forEach((node, index) => {
      const y = 1 - 2 * (index + 0.5) / satellites.length;
      const ring = Math.sqrt(1 - y * y);
      const angle = index * Math.PI * (3 - Math.sqrt(5)) + rotation;
      const x = Math.cos(angle) * ring;
      const z = Math.sin(angle) * ring;
      const perspective = 2.8 / (2.8 - z);
      node.x = cx + x * radius * 0.78 * perspective;
      node.y = cy + (y * 0.8 + z * 0.2) * radius * perspective;
      node.depth = (z + 1) / 2;
      node.radius = 3.5 + perspective * 2.2;
    });
    nodes.sort((a, b) => a.depth - b.depth);
  }
  return { nodes, guides };
}

export function placeNodes(layout: Layout, placements: ReadonlyMap<string, Placement>, width: number, height: number): Layout {
  return {
    ...layout,
    nodes: layout.nodes.map((node) => {
      const placement = placements.get(node.id);
      if (!placement) return node;
      if (placement.kind === "angle" && node.ring) return {
        ...node, x: width / 2 + Math.cos(placement.angle) * node.ring.rx,
        y: height / 2 + Math.sin(placement.angle) * node.ring.ry,
        ring: { ...node.ring, angle: placement.angle },
      };
      if (placement.kind === "offset") return { ...node, x: node.x + placement.x * width, y: node.y + placement.y * height };
      return node;
    }),
  };
}

export function nodePlacement(node: PlotNode, x: number, y: number, width: number, height: number, view: ViewMode): Placement | null {
  if (view === "Ring") {
    if (!node.ring) return null;
    return { kind: "angle", angle: Math.atan2((y - height / 2) / node.ring.ry, (x - width / 2) / node.ring.rx) };
  }
  return { kind: "offset", x: (x - node.x) / width, y: (y - node.y) / height };
}
