import { describe, expect, it } from "vitest";
import { areaSectors, createLayout, nodePlacement, placeNodes, RINGS, ringId, VIEWS } from "./layouts";
import type { Placement } from "./layouts";
import { filterVault, loadDemoVault, ROOT_FILE } from "./vault";

describe("D3 vault layouts", () => {
  const graph = loadDemoVault();

  it.each(VIEWS)("%s places every real file once with finite, bounded coordinates without mutating source links", (view) => {
    const before = JSON.stringify(graph);
    const result = createLayout(graph, view, 1000, 700);
    expect(result.nodes).toHaveLength(graph.files.length);
    expect(new Set(result.nodes.map((node) => node.id)).size).toBe(graph.files.length);
    expect(new Set(result.nodes.map((node) => `${node.x.toFixed(3)},${node.y.toFixed(3)}`)).size).toBe(graph.files.length);
    for (const node of result.nodes) {
      expect(Number.isFinite(node.x) && Number.isFinite(node.y)).toBe(true);
      expect(node.x).toBeGreaterThanOrEqual(0);
      expect(node.x).toBeLessThanOrEqual(1000);
      expect(node.y).toBeGreaterThanOrEqual(0);
      expect(node.y).toBeLessThanOrEqual(700);
    }
    expect(JSON.stringify(graph)).toBe(before);
    expect(createLayout(graph, view, 1000, 700)).toEqual(result);
  });

  it("centers AGENTS.md and groups every capability on its named ring, independent of interest folders", () => {
    const layout = createLayout(graph, "Ring", 1000, 700);
    expect(layout.nodes.find((node) => node.id === ROOT_FILE)).toMatchObject({ x: 500, y: 350 });
    const tracks = layout.guides.filter((guide) => guide.kind === "ellipse");
    expect(tracks).toHaveLength(5);
    expect(tracks.map((guide) => guide.label?.split(" / ")[0])).toEqual(RINGS.map((ring) => ring.label));
    expect(tracks.map((guide) => guide.ring)).toEqual(RINGS.map((ring) => ring.id));
    for (const [index, guide] of tracks.entries()) {
      layout.nodes.filter((node) => node.id !== ROOT_FILE && node.file.kind !== "area" && ringId(node.file) === RINGS[index].id).forEach((node) => {
        expect(Math.hypot((node.x - 500) / node.ring!.rx, (node.y - 350) / node.ring!.ry)).toBeCloseTo(1);
        if (RINGS[index].id !== "knowledge") expect(node.ring!.rx).toBe(guide.rx);
        else expect(node.ring!.rx / 288).toBeGreaterThanOrEqual(0.43);
      });
    }
    expect(tracks[0].label).toBe("Skills / 5");
    expect(tracks[1].label).toBe(`Knowledge / ${graph.files.filter((file) => file.kind === "note").length}`);
    const business = layout.nodes.filter((node) => node.file.area === "business" && node.file.kind === "note");
    expect(new Set(business.map((node) => node.ring!.rx)).size).toBeGreaterThan(1);
  });

  it("weights sectors by area size and puts overview hubs between the core and their note bands", () => {
    const sectors = areaSectors(graph);
    const business = sectors.find((area) => area.id === "business")!;
    const system = sectors.find((area) => area.id === "system")!;
    expect(business.end - business.start).toBeGreaterThan(system.end - system.start);
    const ring = createLayout(graph, "Ring", 1000, 700);
    const hub = ring.nodes.find((node) => node.id === "business/index.md")!;
    expect(hub.radius).toBeGreaterThan(ring.nodes.find((node) => node.file.kind === "note")!.radius * 2);
    expect(hub.ring!.rx).toBeLessThan(ring.nodes.find((node) => node.id === "business/studio.md")!.ring!.rx);
  });

  it("builds six compact Areas neighborhoods instead of one mixed cloud, with applications near the nucleus", () => {
    const layout = createLayout(graph, "Areas", 1000, 700);
    const hubs = layout.nodes.filter((node) => node.file.kind === "area");
    for (const hub of hubs) {
      const members = layout.nodes.filter((node) => node.file.area === hub.file.area && node.file.kind === "note");
      const ownDistance = members.reduce((sum, node) => sum + Math.hypot(node.x - hub.x, node.y - hub.y), 0) / members.length;
      const otherDistance = hubs.filter((other) => other !== hub).reduce((sum, other) => sum + Math.hypot(other.x - hub.x, other.y - hub.y), 0) / (hubs.length - 1);
      expect(ownDistance).toBeLessThan(otherDistance * 0.6);
    }
    expect(layout.nodes.filter((node) => node.file.kind === "application").every((node) => Math.hypot(node.x - 500, node.y - 350) < 150)).toBe(true);
  });

  it("produces six different arrangements rather than relabeling the same view", () => {
    const arrangements = VIEWS.map((view) => JSON.stringify(createLayout(graph, view, 1000, 700).nodes.map(({ id, x, y }) => ({ id, x, y }))));
    expect(new Set(arrangements).size).toBe(6);
  });

  it("uses portrait ellipses on mobile with at least fourteen pixels between ring labels", () => {
    const result = createLayout(graph, "Ring", 300, 580);
    const tracks = result.guides.filter((guide) => guide.kind === "ellipse");
    expect(tracks).toHaveLength(5);
    expect(tracks.every((guide) => guide.ry > guide.rx && guide.label)).toBe(true);
    for (let i = 1; i < tracks.length; i++) {
      expect(tracks[i].ry - tracks[i - 1].ry).toBeGreaterThanOrEqual(14);
    }
    expect(result.nodes.every((node) => node.x >= 0 && node.x <= 300 && node.y >= 0 && node.y <= 580)).toBe(true);
  });

  it("uses actual creation dates in Timeline and moves projected depth in Orbit", () => {
    const timeline = createLayout(graph, "Timeline", 1000, 700);
    const first = timeline.nodes.find((node) => node.id === ROOT_FILE)!;
    const last = timeline.nodes.find((node) => node.id === "career/conversations.md")!;
    expect(first.x).toBeLessThan(last.x);
    const research = timeline.nodes.find((node) => node.id === "skills/research.md")!;
    const writing = timeline.nodes.find((node) => node.id === "skills/writing.md")!;
    expect(research.x).toBe(writing.x);
    expect(Math.abs(research.y - writing.y)).toBeGreaterThanOrEqual(14);
    const original = createLayout(graph, "3D Orbit", 1000, 700);
    const rotated = createLayout(graph, "3D Orbit", 1000, 700, Math.PI / 2);
    const id = "skills/writing.md";
    expect(original.nodes.find((node) => node.id === id)?.x).not.toBe(rotated.nodes.find((node) => node.id === id)?.x);
    expect(new Set(original.nodes.map((node) => node.depth)).size).toBeGreaterThan(20);
    expect(original.guides[0].points).not.toEqual(rotated.guides[0].points);
  });

  it("constrains ring dragging to the assigned ellipse and keeps the entry point anchored", () => {
    const layout = createLayout(graph, "Ring", 300, 580);
    const node = layout.nodes.find((item) => item.id === "skills/research.md")!;
    const placement = nodePlacement(node, 280, 100, 300, 580, "Ring")!;
    const edits = new Map<string, Placement>([[node.id, placement]]);
    const moved = placeNodes(layout, edits, 300, 580).nodes.find((item) => item.id === node.id)!;
    expect(moved.x).not.toBe(node.x);
    expect(Math.hypot((moved.x - 150) / node.ring!.rx, (moved.y - 290) / node.ring!.ry)).toBeCloseTo(1);
    expect(nodePlacement(layout.nodes.find((item) => item.id === ROOT_FILE)!, 1, 1, 300, 580, "Ring")).toBeNull();
    const resized = placeNodes(createLayout(graph, "Ring", 600, 700), edits, 600, 700).nodes.find((item) => item.id === node.id)!;
    expect(resized.ring?.angle).toBe(moved.ring?.angle);
  });

  it("uses normalized free placements outside Ring without changing source layouts", () => {
    const layout = createLayout(graph, "Links", 1000, 700);
    const before = JSON.stringify(layout);
    const node = layout.nodes.find((item) => item.id === "business/atlas-project.md")!;
    const placement = nodePlacement(node, node.x + 70, node.y + 35, 1000, 700, "Links")!;
    const moved = placeNodes(layout, new Map([[node.id, placement]]), 1000, 700).nodes.find((item) => item.id === node.id)!;
    expect(moved.x).toBeCloseTo(node.x + 70);
    expect(moved.y).toBeCloseTo(node.y + 35);
    expect(JSON.stringify(layout)).toBe(before);
  });

  it.each(VIEWS)("%s handles a root-only filtered graph and narrow canvas", (view) => {
    const result = createLayout(filterVault(graph, "unmatched", "all"), view, 280, 580);
    expect(result.nodes).toHaveLength(1);
    expect(Number.isFinite(result.nodes[0].x) && Number.isFinite(result.nodes[0].y)).toBe(true);
    expect(createLayout(graph, view, 280, 580).nodes.every((node) => Number.isFinite(node.x) && Number.isFinite(node.y))).toBe(true);
  });
});
