import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import GraphCanvas from "./GraphCanvas";
import type { GraphResult } from "./api";

const sigma = vi.hoisted(() => {
  const defaults: Record<string, number> = { inertiaDuration: 200, inertiaRatio: 3, zoomDuration: 250, doubleClickZoomingDuration: 200 };
  return {
    constructed: vi.fn(),
    renderer: {
      on: vi.fn(), kill: vi.fn(), refresh: vi.fn(),
      getSetting: vi.fn((key: string) => defaults[key]),
      setSettings: vi.fn(),
    },
  };
});
vi.mock("sigma", () => ({
  default: class FakeSigma {
    constructor(...args: unknown[]) { sigma.constructed(...args); return sigma.renderer; }
  },
}));
vi.mock("sigma/rendering", () => ({ drawDiscNodeHover: vi.fn() }));
const data: GraphResult = {
  entities: [{ id: "tool:logs", type: "tool", label: "Logs", source: { kind: "registry_manifest", id: "tools/logs/manifest.yaml" }, metadata: {} }],
  relationships: [], truncated: false, depth: 0,
};
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe("Sigma graph renderer", () => {
  it("keeps an explicit accessible state without WebGL instead of failing the page", () => {
    vi.stubGlobal("WebGL2RenderingContext", undefined);
    render(<GraphCanvas data={data} selected={null} theme="light" onSelect={vi.fn()} />);
    expect(screen.getByRole("status")).toHaveTextContent("Use Inspect entity");
    expect(screen.getByRole("button", { name: "Zoom in" })).toBeDisabled();
    expect(sigma.constructed).not.toHaveBeenCalled();
  });

  it("updates motion settings, wires canvas selection, and disposes the renderer", async () => {
    vi.stubGlobal("WebGL2RenderingContext", class {});
    const events = new EventTarget();
    const motion = {
      matches: true,
      addEventListener: events.addEventListener.bind(events),
      removeEventListener: events.removeEventListener.bind(events),
    };
    vi.stubGlobal("matchMedia", vi.fn(() => motion));
    const selected = vi.fn();
    const { unmount } = render(<GraphCanvas data={data} selected={null} theme="dark" onSelect={selected} />);
    await waitFor(() => expect(sigma.renderer.setSettings).toHaveBeenCalledWith({
      inertiaDuration: 1, inertiaRatio: 0, zoomDuration: 1, doubleClickZoomingDuration: 1,
    }));
    const callback = sigma.renderer.on.mock.calls.find(([event]) => event === "clickNode")![1] as (event: { node: string }) => void;
    callback({ node: "tool:logs" });
    expect(selected).toHaveBeenCalledWith("tool:logs");
    motion.matches = false;
    events.dispatchEvent(new Event("change"));
    expect(sigma.renderer.setSettings).toHaveBeenLastCalledWith({
      inertiaDuration: 200, inertiaRatio: 3, zoomDuration: 250, doubleClickZoomingDuration: 200,
    });
    unmount();
    expect(sigma.renderer.kill).toHaveBeenCalledOnce();
  });
});
