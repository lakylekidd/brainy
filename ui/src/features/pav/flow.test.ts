import { describe, expect, it } from "vitest";
import { flowProgress, linkControl, linkPoint, signalDuration, signalPath } from "./flow";

describe("directed reference flow", () => {
  const source = { x: 10, y: 30 };
  const target = { x: 210, y: 90 };

  it("uses exactly the same directed quadratic path for the edge, arrow and particle", () => {
    expect(linkPoint(source, target, 0)).toEqual(source);
    expect(linkPoint(source, target, 1)).toEqual(target);
    const control = linkControl(source, target);
    expect(linkPoint(source, target, 0.5)).toEqual({
      x: (source.x + 2 * control.x + target.x) / 4,
      y: (source.y + 2 * control.y + target.y) / 4,
    });
    expect(linkPoint(target, source, 0)).toEqual(target);
    expect(linkPoint(target, source, 1)).toEqual(source);
  });

  it("advances from source to target, wraps in a bounded loop and staggers references", () => {
    expect(flowProgress(0, 0)).toBe(0);
    expect(flowProgress(850, 0)).toBeCloseTo(0.25);
    expect(flowProgress(1700, 0)).toBeCloseTo(0.5);
    expect(flowProgress(3400, 0)).toBe(0);
    expect(linkPoint(source, target, flowProgress(850, 0)).x)
      .toBeLessThan(linkPoint(source, target, flowProgress(1700, 0)).x);
    expect(flowProgress(850, 1)).not.toBe(flowProgress(850, 0));
    expect(flowProgress(1e9, 83)).toBeGreaterThanOrEqual(0);
    expect(flowProgress(1e9, 83)).toBeLessThan(1);
  });

  it("travels a deep Circle bow by arc length instead of uneven curve-parameter speed", () => {
    const path = signalPath(source, target, { x: 100, y: 400 });
    expect(path.at(0)).toEqual(source);
    expect(path.at(1)).toEqual(target);
    expect(path.control).not.toEqual(linkControl(source, target));
    const quarters = Array.from({ length: 4 }, (_, quarter) => {
      let length = 0;
      for (let i = 1; i <= 20; i++) {
        const a = path.at(quarter / 4 + (i - 1) / 80);
        const b = path.at(quarter / 4 + i / 80);
        length += Math.hypot(b.x - a.x, b.y - a.y);
      }
      return length;
    });
    expect(Math.max(...quarters) - Math.min(...quarters)).toBeLessThan(0.5);
    expect(path.at(-1)).toEqual(source);
    expect(path.at(2)).toEqual(target);
  });

  it("bounds travel to 1.6-6 seconds, respects world distance and safely handles coincident nodes", () => {
    expect(signalDuration(20, 1)).toBe(1600);
    expect(signalDuration(180, 1)).toBe(3000);
    expect(signalDuration(180, 2)).toBe(3000);
    expect(signalDuration(5000, 4)).toBe(6000);
    const path = signalPath(source, source);
    expect(path.length).toBe(0);
    expect(path.at(0.5)).toEqual(source);
  });
});
