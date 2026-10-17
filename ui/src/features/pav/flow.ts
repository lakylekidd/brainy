export interface Point { x: number; y: number }
export function linkControl(source: Point, target: Point): Point {
  return { x: (source.x + target.x) / 2 - (target.y - source.y) * 0.12, y: (source.y + target.y) / 2 + (target.x - source.x) * 0.12 };
}

export function linkPoint(source: Point, target: Point, t: number): Point {
  const control = linkControl(source, target);
  const u = 1 - t;
  return {
    x: u * u * source.x + 2 * u * t * control.x + t * t * target.x,
    y: u * u * source.y + 2 * u * t * control.y + t * t * target.y,
  };
}

export function flowProgress(elapsed: number, index: number): number {
  return ((elapsed / 3400 + index * 0.381966) % 1 + 1) % 1;
}
