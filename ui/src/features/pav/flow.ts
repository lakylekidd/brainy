export interface Point { x: number; y: number }
export function linkControl(source: Point, target: Point, center?: Point): Point {
  if (center) return { x: center.x + (source.x + target.x - 2 * center.x) * 0.12, y: center.y + (source.y + target.y - 2 * center.y) * 0.12 };
  return { x: (source.x + target.x) / 2 - (target.y - source.y) * 0.12, y: (source.y + target.y) / 2 + (target.x - source.x) * 0.12 };
}

export function linkPoint(source: Point, target: Point, t: number, control = linkControl(source, target)): Point {
  const u = 1 - t;
  return {
    x: u * u * source.x + 2 * u * t * control.x + t * t * target.x,
    y: u * u * source.y + 2 * u * t * control.y + t * t * target.y,
  };
}

export function flowProgress(elapsed: number, index: number, duration = 3400): number {
  return ((elapsed / duration + index * 0.381966) % 1 + 1) % 1;
}

export function signalPath(source: Point, target: Point, center?: Point) {
  const control = linkControl(source, target, center);
  const samples = Array.from({ length: 21 }, (_, i) => linkPoint(source, target, i / 20, control));
  const distances = [0];
  for (let i = 1; i < samples.length; i++) distances.push(distances[i - 1] + Math.hypot(samples[i].x - samples[i - 1].x, samples[i].y - samples[i - 1].y));
  const length = distances.at(-1)! < 1e-9 ? 0 : distances.at(-1)!;
  return {
    control, length,
    at(progress: number): Point {
      if (length === 0) return { ...source };
      const distance = Math.max(0, Math.min(1, progress)) * length;
      const end = Math.min(20, Math.max(1, distances.findIndex((value) => value >= distance)));
      const start = distances[end - 1];
      const t = (end - 1 + (distance - start) / Math.max(1e-9, distances[end] - start)) / 20;
      return linkPoint(source, target, t, control);
    },
  };
}

export function signalDuration(worldLength: number, zoom: number): number {
  return Math.max(1600, Math.min(6000, Math.max(worldLength / 60, worldLength * zoom / 230) * 1000));
}

export function drawSignal(ctx: CanvasRenderingContext2D, path: ReturnType<typeof signalPath>, progress: number, color: string, alpha: number, zoom: number) {
  const tail = Math.min(0.22, 22 / Math.max(1, path.length * zoom));
  const start = Math.max(0, progress - tail);
  ctx.save();
  ctx.strokeStyle = color;
  ctx.lineCap = "round";
  for (let i = 0; i < 7; i++) {
    const a = path.at(start + (progress - start) * i / 7);
    const b = path.at(start + (progress - start) * (i + 1) / 7);
    ctx.globalAlpha = alpha * (0.035 + 0.66 * ((i + 1) / 7) ** 2);
    ctx.lineWidth = 1.5 / zoom;
    ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
  }
  const head = path.at(progress);
  const radius = 4.8 / zoom;
  const glow = ctx.createRadialGradient(head.x, head.y, 0, head.x, head.y, radius);
  glow.addColorStop(0, `${color}a0`); glow.addColorStop(1, `${color}00`);
  ctx.globalAlpha = alpha;
  ctx.fillStyle = glow;
  ctx.fillRect(head.x - radius, head.y - radius, radius * 2, radius * 2);
  ctx.beginPath(); ctx.arc(head.x, head.y, 1.15 / zoom, 0, Math.PI * 2); ctx.fillStyle = color; ctx.fill();
  ctx.beginPath(); ctx.arc(head.x, head.y, 0.48 / zoom, 0, Math.PI * 2); ctx.fillStyle = "#fff0d8"; ctx.fill();
  ctx.restore();
}
