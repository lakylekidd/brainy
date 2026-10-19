import {
  forceCollide, forceLink, forceManyBody, forceSimulation, forceX, forceY, scaleUtc, utcDay,
} from "d3";
import type { SimulationNodeDatum } from "d3";
import { AREAS, ROOT_FILE, type VaultFile, type VaultGraph } from "./vault";

export const VIEWS = ["Ring", "Circle", "Areas", "Links", "Timeline", "3D Orbit"] as const;
export type ViewMode = typeof VIEWS[number];
export const RINGS = [
  { id: "skills", label: "Skills", fraction: 0.27, color: "#f07a2e" },
  { id: "knowledge", label: "Knowledge", fraction: 0.52, color: "#81919b" },
  { id: "memory", label: "Memory", fraction: 0.71, color: "#a874f0" },
  { id: "routines", label: "Routines", fraction: 0.86, color: "#d9a445" },
  { id: "applications", label: "Applications", fraction: 1, color: "#7fa3cc" },
] as const;
export type RingId = typeof RINGS[number]["id"];
export function ringId(file: VaultFile): RingId {
  return RINGS.find((ring) => ring.id === file.layer)?.id ?? "knowledge";
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
  ring?: RingId;
  color?: string;
  points?: { x: number; y: number }[];
}
export interface Layout { nodes: PlotNode[]; guides: Guide[] }
const TAU = Math.PI * 2;

export function areaSectors(graph: VaultGraph) {
  const groups = AREAS.filter((area) => graph.files.some((file) => file.area === area.id)).map((area) => ({
    ...area, files: graph.files.filter((file) => file.area === area.id && file.kind !== "application"),
  }));
  const weights = groups.map((area) => Math.max(2.6, Math.sqrt(area.files.length)));
  const total = weights.reduce((sum, value) => sum + value, 0);
  let start = -Math.PI / 2 + 0.15;
  return groups.map((area, index) => {
    const end = start + (TAU - 0.3) * weights[index] / total;
    const result = { ...area, start, end, mid: (start + end) / 2 };
    start = end;
    return result;
  });
}

export function createLayout(graph: VaultGraph, view: ViewMode, width: number, height: number, rotation = 0, sway = 0): Layout {
  const cx = width / 2;
  const cy = height / 2;
  const verticalInset = view === "Ring" ? 28 : 62;
  const radius = Math.max(50, Math.min(width / 2 - (width < 500 ? 24 : 55), height / 2 - verticalInset));
  const ringHeight = view === "Ring" ? Math.max(radius, Math.min(height / 2 - verticalInset, radius * 1.9)) : radius;
  const groups = areaSectors(graph);
  const nodes: PlotNode[] = graph.files.map((file) => ({
    id: file.id, file, x: cx, y: cy, radius: file.id === ROOT_FILE
      ? (view === "Timeline" ? 12 : Math.max(19, Math.min(125, radius * 0.19)))
      : file.kind === "area" ? 8 : file.kind === "application" ? 7 : file.kind === "routine" ? 4.6 : 3.2, depth: 1,
  }));
  const guides: Guide[] = [];
  const satellites = nodes.filter((node) => node.id !== ROOT_FILE);
  const setPolar = (node: PlotNode, fraction: number, angle: number) => {
    const rx = radius * fraction;
    const ry = ringHeight * fraction;
    node.x = cx + Math.cos(angle) * rx;
    node.y = cy + Math.sin(angle) * ry;
    if (view === "Ring") node.ring = { rx, ry, angle };
  };
  RINGS.forEach((ring) => {
    const members = satellites.filter((node) => node.file.kind !== "area" && ringId(node.file) === ring.id);
    const r = radius * ring.fraction;
    const ry = ringHeight * ring.fraction;
    groups.forEach((area) => {
      const peers = members.filter((node) => node.file.area === area.id);
      const pad = Math.min(0.05, (area.end - area.start) * 0.12);
      const span = area.end - area.start - pad * 2;
      const rows = ring.id === "knowledge" ? Math.min(4, Math.max(1, Math.ceil(peers.length * 10 / (span * radius * ring.fraction)))) : 1;
      const perRow = Math.ceil(peers.length / rows);
      peers.forEach((node, i) => {
        const row = Math.floor(i / perRow);
        const count = Math.min(perRow, peers.length - row * perRow);
        const angle = area.start + pad + span * ((i % perRow) + (perRow - count) / 2 + 0.5) / perRow;
        const fraction = rows === 1 ? ring.fraction : 0.43 + row / (rows - 1) * 0.2;
        setPolar(node, fraction, angle);
      });
    });
    if (ring.id === "applications") members.forEach((node, i) => setPolar(node, 1, -Math.PI / 2 + (i + 0.5) / members.length * TAU));
    if (view === "Ring" && members.length) guides.push({
      kind: "ellipse", x: cx, y: cy, rx: r, ry,
      label: `${ring.label} / ${members.length}`, color: ring.color, ring: ring.id,
    });
  });
  groups.forEach((area) => {
    const hub = nodes.find((node) => node.file.area === area.id && node.file.kind === "area");
    if (hub) setPolar(hub, 0.345, area.mid);
  });
  if (view === "Ring") {
    groups.filter((area) => !nodes.some((node) => node.file.kind === "area" && node.file.area === area.id)).forEach((area) => {
      guides.push({
        kind: "label", x: cx + Math.cos(area.mid) * radius * 0.35, y: cy + Math.sin(area.mid) * ringHeight * 0.35,
        rx: 0, ry: 0, label: area.label, color: area.color,
      });
    });
  }

  if (view === "Circle") {
    let start = -Math.PI / 2;
    const span = TAU - groups.length * 0.06;
    groups.forEach((group) => {
      const members = satellites.filter((node) => node.file.area === group.id);
      const arc = span * members.length / Math.max(1, satellites.length);
      members.forEach((node, index) => {
        const angle = start + arc * (index + 0.5) / members.length;
        node.x = cx + Math.cos(angle) * radius;
        node.y = cy + Math.sin(angle) * radius;
      });
      const mid = start + arc / 2;
      guides.push({ kind: "label", x: cx + Math.cos(mid) * radius * 1.1, y: cy + Math.sin(mid) * radius * 1.1,
        rx: 0, ry: 0, label: group.label, color: group.color });
      start += arc + 0.06;
    });
    guides.push({ kind: "ellipse", x: cx, y: cy, rx: radius, ry: radius });
  }

  if ((view === "Areas" || view === "Links") && nodes.length) {
    const areas = view === "Areas";
    const centers = new Map(groups.map((group) => {
      const r = radius * (0.54 + 0.22 * Math.min(1, Math.sqrt(group.files.length) / 14)) * (areas ? 1 : 1.3);
      return [group.id, { x: cx + Math.cos(group.mid) * r, y: cy + Math.sin(group.mid) * r }];
    }));
    const applications = satellites.filter((node) => node.file.kind === "application");
    const anchors = new Map(applications.map((node, index) => {
      const angle = -Math.PI / 2 + index / applications.length * TAU;
      const r = radius * (areas ? 0.26 : 0.13);
      return [node.id, { x: cx + Math.cos(angle) * r, y: cy + Math.sin(angle) * r }];
    }));
    const anchor = (node: PlotNode) => anchors.get(node.id) ?? (node.file.area ? centers.get(node.file.area) : undefined) ?? { x: cx, y: cy };
    groups.forEach((group) => {
      const center = centers.get(group.id)!;
      nodes.filter((node) => node.file.area === group.id && node.file.kind !== "application").forEach((node, i) => {
        const angle = i * Math.PI * (3 - Math.sqrt(5));
        const spread = node.file.kind === "area" ? 0 : 5 * Math.sqrt(i + 1);
        node.x = center.x + Math.cos(angle) * spread;
        node.y = center.y + Math.sin(angle) * spread;
      });
    });
    const root = nodes.find((node) => node.id === ROOT_FILE);
    if (root) { root.fx = cx; root.fy = cy; }
    // Stop the simulation before ticking so no timer or mutable D3 state escapes the layout.
    const simulation = forceSimulation(nodes).stop()
      .force("links", forceLink<PlotNode, { source: string; target: string; membership: boolean }>([
        ...graph.links.map((link) => ({ ...link, membership: false })),
        ...graph.memberships.map((link) => ({ ...link, membership: true })),
      ]).id((node) => node.id).distance((link) => link.membership ? (areas ? 18 : 26) : areas ? 34 : 40)
        .strength((link) => link.membership ? (areas ? 0.08 : 0.32) : areas ? 0.05 : 0.22))
      .force("charge", forceManyBody<PlotNode>().strength((node) => areas ? (node.file.kind === "application" ? -34 : -6) : node.file.kind === "area" ? -60 : -16)
        .distanceMax(areas ? 100 : 240))
      .force("collide", forceCollide<PlotNode>((node) => node.file.kind === "application"
        ? node.radius * (areas ? 1.9 : 1.8) + (areas ? 3 : 2) : node.radius + (areas ? 1.6 : 1.2)).iterations(areas ? 2 : 1))
      .force("x", forceX<PlotNode>((node) => anchor(node).x).strength((node) => areas ? (anchors.has(node.id) || node.file.kind === "area" ? 0.26 : 0.11) : 0.1))
      .force("y", forceY<PlotNode>((node) => anchor(node).y).strength((node) => areas ? (anchors.has(node.id) || node.file.kind === "area" ? 0.26 : 0.11) : 0.1))
      .alphaDecay(areas ? 0.022 : 0.02);
    simulation.tick(areas ? 220 : 260);
    const spread = Math.max(1, ...nodes.map((node) => Math.max(Math.abs(node.x - cx), Math.abs(node.y - cy))));
    const fit = Math.min(areas ? 2 : 1, radius / spread);
    nodes.forEach((node) => { node.x = cx + (node.x - cx) * fit; node.y = cy + (node.y - cy) * fit; });
    if (areas) groups.forEach((group) => {
      const members = nodes.filter((node) => node.file.area === group.id);
      if (members.some((node) => node.file.kind === "area")) return;
      const x = members.reduce((sum, node) => sum + node.x, 0) / members.length;
      guides.push({ kind: "label", x, y: Math.min(...members.map((node) => node.y)) - 16, rx: 0, ry: 0, label: group.label, color: group.color });
    });
  }

  if (view === "Timeline") {
    const dates = graph.files.map((file) => new Date(`${file.created}T00:00:00Z`));
    const first = new Date(Math.min(...dates.map(Number)));
    const last = new Date(Math.max(...dates.map(Number)));
    const scale = scaleUtc().domain([utcDay.offset(first, -1), utcDay.offset(last, 1)]).range([90, width - 65]);
    const lanes = ["core", ...RINGS.map((ring) => ring.id)];
    const step = (height - 115) / Math.max(1, lanes.length - 1);
    lanes.forEach((group, index) => {
      const y = 44 + index * step;
      guides.push({ kind: "line", x: 90, y, rx: width - 155, ry: 0 });
      guides.push({ kind: "label", x: 8, y: y - 12, rx: 0, ry: 0, label: group === "core" ? "Brain / Areas" : RINGS.find((item) => item.id === group)?.label });
    });
    nodes.forEach((node) => {
      const peers = nodes.filter((item) => item.file.layer === node.file.layer && item.file.created === node.file.created);
      const spacing = Math.min(16, step * 0.6 / Math.max(1, peers.length - 1));
      node.x = scale(new Date(`${node.file.created}T00:00:00Z`));
      node.y = 44 + lanes.indexOf(node.file.layer) * step + (peers.indexOf(node) - (peers.length - 1) / 2) * spacing;
    });
    scale.ticks(Math.max(2, Math.floor(width / 160))).forEach((tick) => guides.push({
      kind: "label", x: scale(tick), y: height - 27, rx: 0, ry: 0,
      label: tick.toISOString().slice(5, 10).replace("-", "/"),
    }));
  }

  if (view === "3D Orbit") {
    const project = (x: number, y: number, z: number) => {
      const yaw = 0.6 + rotation;
      const tilt = 0.42 + sway;
      const rotatedX = x * Math.cos(yaw) + z * Math.sin(yaw);
      const rotatedZ = z * Math.cos(yaw) - x * Math.sin(yaw);
      const depth = y * Math.sin(tilt) + rotatedZ * Math.cos(tilt);
      const perspective = 2 / (2 + depth);
      return { x: cx + rotatedX * radius * perspective,
        y: cy + (y * Math.cos(tilt) - rotatedZ * Math.sin(tilt)) * radius * perspective,
        depth: Math.max(0, Math.min(1, (1 - depth / 0.86) / 2)), perspective };
    };
    const shells: Record<RingId, number> = { skills: 0.42, knowledge: 0.68, memory: 0.68, routines: 0.86, applications: 1 };
    RINGS.forEach((ring) => {
      const members = satellites.filter((node) => ringId(node.file) === ring.id);
      groups.forEach((area) => {
        const peers = members.filter((node) => node.file.area === area.id);
        peers.forEach((node, i) => {
          const latitude = ring.id === "applications" ? 0 : ring.id === "skills" ? -0.8 + (i + 0.5) / peers.length * 0.6
            : ring.id === "routines" ? 0.2 + (i + 0.5) / peers.length * 0.6 : -0.92 + (i + 0.5) / peers.length * 1.84;
          const angle = area.start + (area.end - area.start) * ((i * 0.6180339887) % 1);
          const shell = shells[ring.id];
          const radial = Math.sqrt(1 - latitude * latitude) * shell;
          const projected = project(Math.cos(angle) * radial, latitude * shell, Math.sin(angle) * radial);
          node.x = projected.x; node.y = projected.y; node.depth = projected.depth;
          node.radius *= Math.min(1.4, projected.perspective);
        });
      });
      const points = Array.from({ length: 97 }, (_, i) => {
        const angle = i / 96 * TAU;
        return project(Math.cos(angle) * shells[ring.id], 0, Math.sin(angle) * shells[ring.id]);
      });
      guides.push({
        kind: "path", x: cx, y: cy, rx: 0, ry: 0, color: ring.color, points,
      });
    });
    groups.forEach((group) => {
      const longitude = group.start;
      guides.push({ kind: "path", x: cx, y: cy, rx: 0, ry: 0, color: group.color,
        points: Array.from({ length: 41 }, (_, i) => {
          const y = 1 - i / 20;
          const radial = Math.sqrt(Math.max(0, 1 - y * y));
          return project(Math.cos(longitude) * radial * 0.86, y * 0.86, Math.sin(longitude) * radial * 0.86);
        }) });
    });
    nodes.sort((a, b) => a.depth - b.depth);
  }
  return { nodes, guides };
}

export function placeNodes(layout: Layout, placements: ReadonlyMap<string, Placement>, width: number, height: number, view?: ViewMode): Layout {
  const areaOffsets = new Map<string, { x: number; y: number; hubX: number; hubY: number; hubRadius: number }>();
  if (view === "Areas") layout.nodes.forEach((node) => {
    const placement = placements.get(node.id);
    if (node.file.kind === "area" && placement?.kind === "offset" && node.file.area) {
      const deltaX = placement.x * width;
      const deltaY = placement.y * height;
      const distance = Math.hypot(deltaX, deltaY);
      const follow = Math.max(0, Math.min(1, (distance - 48) / 120));
      areaOffsets.set(node.file.area, {
        x: deltaX * follow,
        y: deltaY * follow,
        hubX: node.x + deltaX,
        hubY: node.y + deltaY,
        hubRadius: node.radius,
      });
    }
  });
  return {
    ...layout,
    nodes: layout.nodes.map((node) => {
      const placement = placements.get(node.id);
      if (placement?.kind === "angle" && node.ring) return {
        ...node, x: width / 2 + Math.cos(placement.angle) * node.ring.rx,
        y: height / 2 + Math.sin(placement.angle) * node.ring.ry,
        ring: { ...node.ring, angle: placement.angle },
      };
      const groupOffset = node.file.kind === "area" || !node.file.area ? undefined : areaOffsets.get(node.file.area);
      if (placement?.kind === "offset" || groupOffset) {
        let x = node.x + (placement?.kind === "offset" ? placement.x * width : 0) + (groupOffset?.x ?? 0);
        let y = node.y + (placement?.kind === "offset" ? placement.y * height : 0) + (groupOffset?.y ?? 0);
        if (groupOffset) {
          const dx = x - groupOffset.hubX;
          const dy = y - groupOffset.hubY;
          const distance = Math.hypot(dx, dy);
          const minimum = groupOffset.hubRadius + node.radius + 12;
          if (distance < minimum) {
            const angle = distance > 0.001 ? Math.atan2(dy, dx) : node.id.split("").reduce((sum, char) => sum + char.charCodeAt(0), 0) % (Math.PI * 2);
            x += Math.cos(angle) * (minimum - distance);
            y += Math.sin(angle) * (minimum - distance);
          }
        }
        return { ...node, x, y };
      }
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
