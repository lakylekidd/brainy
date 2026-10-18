import { describe, expect, it } from "vitest";
import { createLayout } from "./layouts";
import { MAP_MOTION, rippleLayout, rippleScale, transitionLayout } from "./motion";
import { loadDemoVault, ROOT_FILE } from "./vault";

describe("reference-style graph motion", () => {
  const graph = loadDemoVault();

  it("uses a 57-second orbit, 27-second tilt sway and an 820ms layout transition", () => {
    expect(Math.PI * 2 / MAP_MOTION.orbitSpeed / 1000).toBeCloseTo(57.12, 2);
    expect(MAP_MOTION.swayPeriod).toBe(27000);
    expect(MAP_MOTION.orbitSway).toBe(0.055);
    expect(MAP_MOTION.transition).toBe(820);
    expect(MAP_MOTION.ambientSignals).toBe(8);
    expect(MAP_MOTION.phoneSignals).toBe(4);
  });

  it("keeps the breathing wave within 1.1 percent and has no displacement before motion starts", () => {
    expect(rippleScale(200, 0)).toBe(1);
    for (let elapsed = 100; elapsed < 10000; elapsed += 100) {
      expect(rippleScale(200, elapsed)).toBeGreaterThanOrEqual(0.989);
      expect(rippleScale(200, elapsed)).toBeLessThanOrEqual(1.011);
    }
  });

  it("moves nodes and their named ellipse coherently without moving AGENTS.md or changing the source", () => {
    const layout = createLayout(graph, "Ring", 300, 580);
    const before = JSON.stringify(layout);
    const result = rippleLayout(layout, 1900, 300, 580);
    expect(result.nodes.find((node) => node.id === ROOT_FILE)).toEqual(layout.nodes.find((node) => node.id === ROOT_FILE));
    const node = result.nodes.find((item) => item.id === "skills/research.md")!;
    const guide = result.guides.find((item) => item.label === "Skills / 5")!;
    expect(Math.hypot((node.x - 150) / guide.rx, (node.y - 290) / guide.ry)).toBeCloseTo(1);
    expect(node.ring?.rx).toBe(guide.rx);
    expect(node.ring?.ry).toBe(guide.ry);
    expect(JSON.stringify(layout)).toBe(before);
  });

  it("eases from the last visible arrangement, staggers areas and settles at the exact target", () => {
    const ring = createLayout(graph, "Ring", 1000, 700);
    const circle = createLayout(graph, "Circle", 1000, 700);
    const from = new Map(ring.nodes.map((node) => [node.id, node]));
    const first = transitionLayout(circle, from, 0);
    const middle = transitionLayout(circle, from, 410);
    const last = transitionLayout(circle, from, 980);
    for (const [i, node] of circle.nodes.entries()) {
      expect(first.nodes[i].x).toBe(from.get(node.id)!.x);
      expect(first.nodes[i].y).toBe(from.get(node.id)!.y);
      expect(last.nodes[i].x).toBeCloseTo(node.x);
      expect(last.nodes[i].y).toBeCloseTo(node.y);
      expect(middle.nodes[i].x).toBeGreaterThanOrEqual(Math.min(node.x, from.get(node.id)!.x) - 1e-9);
      expect(middle.nodes[i].x).toBeLessThanOrEqual(Math.max(node.x, from.get(node.id)!.x) + 1e-9);
    }
    expect(middle.nodes).not.toEqual(first.nodes);
    expect(middle.nodes).not.toEqual(last.nodes);
  });
});
