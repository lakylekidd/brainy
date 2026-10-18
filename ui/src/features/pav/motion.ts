import { easeCubicInOut } from "d3";
import { canvasFont } from "../../appearance";
import type { Layout, PlotNode } from "./layouts";
import { fileColor, AREAS, ROOT_FILE } from "./vault";

export const MAP_MOTION = {
  orbitSpeed: 0.00011,
  orbitSway: 0.055,
  swayPeriod: 27000,
  rippleAmplitude: 0.011,
  rippleFrequency: 0.9,
  rippleWavelength: 0.011,
  transition: 820,
  ambientSignals: 8,
  phoneSignals: 4,
} as const;

export function rippleScale(distance: number, elapsed: number): number {
  return elapsed === 0 ? 1 : 1 + MAP_MOTION.rippleAmplitude
    * Math.sin(elapsed / 1000 * MAP_MOTION.rippleFrequency - distance * MAP_MOTION.rippleWavelength);
}

export function rippleLayout(layout: Layout, elapsed: number, width: number, height: number): Layout {
  const cx = width / 2;
  const cy = height / 2;
  return {
    nodes: layout.nodes.map((node) => {
      if (node.id === ROOT_FILE) return node;
      const distance = node.ring ? (node.ring.rx + node.ring.ry) / 2 : Math.hypot(node.x - cx, node.y - cy);
      const scale = rippleScale(distance, elapsed);
      return { ...node, x: cx + (node.x - cx) * scale, y: cy + (node.y - cy) * scale,
        ring: node.ring && { ...node.ring, rx: node.ring.rx * scale, ry: node.ring.ry * scale } };
    }),
    guides: layout.guides.map((guide) => {
      if (guide.kind !== "ellipse") return guide;
      const scale = rippleScale((guide.rx + guide.ry) / 2, elapsed);
      return { ...guide, rx: guide.rx * scale, ry: guide.ry * scale };
    }),
  };
}

export function transitionLayout(layout: Layout, from: ReadonlyMap<string, { x: number; y: number }>, elapsed: number): Layout {
  return { ...layout, nodes: layout.nodes.map((node) => {
    const previous = from.get(node.id);
    if (!previous) return node;
    const delay = Math.min(160, Math.max(0, AREAS.findIndex((area) => area.id === node.file.area)) * 22);
    const t = easeCubicInOut(Math.max(0, Math.min(1, (elapsed - delay) / MAP_MOTION.transition)));
    return { ...node, x: previous.x + (node.x - previous.x) * t, y: previous.y + (node.y - previous.y) * t };
  }) };
}

const corePoints = Array.from({ length: 360 }, (_, i) => {
  const z = 1 - 2 * (i + 0.5) / 360;
  const angle = i * Math.PI * (3 - Math.sqrt(5));
  const fraction = Math.sin((i + 1) * 127.1) * 43758.5453;
  const radius = Math.cbrt(fraction - Math.floor(fraction));
  return { x: Math.cos(angle) * Math.sqrt(1 - z * z) * radius,
    y: Math.sin(angle) * Math.sqrt(1 - z * z) * radius, z: z * radius };
});
const coreEdges = corePoints.flatMap((point, i) => corePoints.map((other, j) => ({
  j, distance: (point.x - other.x) ** 2 + (point.y - other.y) ** 2 + (point.z - other.z) ** 2,
})).filter((other) => other.j !== i).sort((a, b) => a.distance - b.distance)
  .slice(0, 2).filter((other) => other.j > i).map((other) => [i, other.j] as const));

export function drawCore(ctx: CanvasRenderingContext2D, node: PlotNode, elapsed: number, surface: string, active: boolean) {
  const t = elapsed / 1000;
  const radius = node.radius * (1 + 0.025 * Math.sin(t * Math.PI * 2 / 7));
  const yaw = t * 0.11;
  const tilt = 0.38 + Math.sin(t * 0.09) * 0.1;
  const color = fileColor(node.file);
  const points = corePoints.map((point) => {
    const x = point.x * Math.cos(yaw) + point.z * Math.sin(yaw);
    const z = point.z * Math.cos(yaw) - point.x * Math.sin(yaw);
    return { x: node.x + x * radius, y: node.y + (point.y * Math.cos(tilt) - z * Math.sin(tilt)) * radius,
      depth: (point.y * Math.sin(tilt) + z * Math.cos(tilt) + 1) / 2 };
  });
  ctx.save();
  const halo = ctx.createRadialGradient(node.x, node.y, 0, node.x, node.y, radius * 1.8);
  halo.addColorStop(0, `${color}65`);
  halo.addColorStop(0.45, `${color}25`);
  halo.addColorStop(1, `${color}00`);
  ctx.fillStyle = halo;
  ctx.fillRect(node.x - radius * 1.8, node.y - radius * 1.8, radius * 3.6, radius * 3.6);
  ctx.strokeStyle = `${color}60`;
  ctx.lineWidth = 0.6;
  ctx.beginPath();
  for (const [i, j] of coreEdges) { ctx.moveTo(points[i].x, points[i].y); ctx.lineTo(points[j].x, points[j].y); }
  ctx.stroke();
  ctx.fillStyle = color;
  points.sort((a, b) => a.depth - b.depth).forEach((point) => {
    ctx.globalAlpha = 0.4 + point.depth * 0.6;
    ctx.beginPath();
    ctx.arc(point.x, point.y, 0.5 + point.depth * 0.8, 0, Math.PI * 2);
    ctx.fill();
  });
  ctx.globalAlpha = 1;
  if (active) {
    ctx.beginPath();
    ctx.arc(node.x, node.y, radius + 4, 0, Math.PI * 2);
    ctx.strokeStyle = `${color}90`;
    ctx.lineWidth = 1;
    ctx.stroke();
  }
  ctx.font = `600 9px ${canvasFont}`;
  const width = ctx.measureText(ROOT_FILE).width;
  ctx.fillStyle = surface;
  ctx.fillRect(node.x - width / 2 - 6, node.y - 8, width + 12, 16);
  ctx.fillStyle = color;
  ctx.textAlign = "center";
  ctx.fillText(ROOT_FILE, node.x, node.y + 3);
  ctx.restore();
}

export function drawGround(ctx: CanvasRenderingContext2D, width: number, height: number, dark: boolean) {
  ctx.strokeStyle = dark ? "#81919b0b" : "#60758010";
  ctx.lineWidth = 0.7;
  ctx.beginPath();
  for (let row = -1; row * 23 < height + 23; row++) {
    for (let column = -1; column * 39 < width + 39; column++) {
      const x = column * 39 + (row % 2 ? 19.5 : 0);
      const y = row * 23;
      for (let side = 0; side <= 6; side++) {
        const angle = side * Math.PI / 3;
        const px = x + Math.cos(angle) * 13;
        const py = y + Math.sin(angle) * 13;
        if (side === 0) ctx.moveTo(px, py);
        else ctx.lineTo(px, py);
      }
    }
  }
  ctx.stroke();
}

export function drawFields(ctx: CanvasRenderingContext2D, layout: Layout, elapsed: number, radius: number) {
  for (const [index, group] of AREAS.entries()) {
    const members = layout.nodes.filter((node) => node.file.area === group.id && node.file.kind !== "application");
    if (!members.length) continue;
    const x = members.reduce((sum, node) => sum + node.x, 0) / members.length;
    const y = members.reduce((sum, node) => sum + node.y, 0) / members.length;
    const breath = 1 + 0.2 * Math.sin(elapsed / 1000 * Math.PI / 4 + index * 1.1);
    const size = radius * (1 + 0.05 * (breath - 1));
    const gradient = ctx.createRadialGradient(x, y, 0, x, y, size);
    gradient.addColorStop(0, `${group.color}${Math.round(12 * breath).toString(16).padStart(2, "0")}`);
    gradient.addColorStop(1, `${group.color}00`);
    ctx.fillStyle = gradient;
    ctx.fillRect(x - size, y - size, size * 2, size * 2);
  }
}
