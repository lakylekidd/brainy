import { describe, expect, it } from "vitest";
import { flowProgress, linkControl, linkPoint } from "./flow";

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
});
