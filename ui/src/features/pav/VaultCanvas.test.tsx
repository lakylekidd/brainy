import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { zoomTransform } from "d3";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createLayout } from "./layouts";
import * as layouts from "./layouts";
import { loadDemoVault, ROOT_FILE } from "./vault";
import VaultCanvas from "./VaultCanvas";

const graph = loadDemoVault();
const context = {
  setTransform: vi.fn(), clearRect: vi.fn(), save: vi.fn(), restore: vi.fn(), translate: vi.fn(), scale: vi.fn(),
  beginPath: vi.fn(), closePath: vi.fn(), ellipse: vi.fn(), fill: vi.fn(), moveTo: vi.fn(), lineTo: vi.fn(), stroke: vi.fn(),
  fillRect: vi.fn(), fillText: vi.fn(), quadraticCurveTo: vi.fn(), arc: vi.fn(),
  setLineDash: vi.fn(), createRadialGradient: vi.fn(() => ({ addColorStop: vi.fn() })),
  measureText: vi.fn((text: string) => ({ width: text.length * 7 })),
};
const disconnect = vi.fn();
beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(context as unknown as CanvasRenderingContext2D);
  vi.stubGlobal("ResizeObserver", class {
    observe = vi.fn();
    disconnect = disconnect;
  });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("D3 HTML Canvas renderer", () => {
  it("draws every file, selects a real dot, zooms and resets its camera, and disposes resizing", () => {
    const onSelect = vi.fn();
    const { unmount } = render(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={onSelect} theme="light" showLinks showLabels={false} />);
    const canvas = screen.getByRole("img", { name: new RegExp(`^Ring view: ${graph.files.length} real Markdown files`) });
    expect(context.arc).toHaveBeenCalled();
    expect(context.quadraticCurveTo).toHaveBeenCalled();
    const layout = createLayout(graph, "Ring", 240, 420);
    const node = layout.nodes.find((item) => item.id === "skills/research.md")!;
    fireEvent.click(canvas, { clientX: node.x, clientY: node.y });
    expect(onSelect).toHaveBeenCalledWith("skills/research.md");
    fireEvent.click(screen.getByRole("button", { name: "Zoom in" }));
    expect(context.scale).toHaveBeenLastCalledWith(1.25, 1.25);
    fireEvent.click(screen.getByRole("button", { name: "Fit vault" }));
    expect(context.scale).toHaveBeenLastCalledWith(1, 1);
    unmount();
    expect(disconnect).toHaveBeenCalled();
  });

  it("draws no edges when links are off and renders file labels on request", () => {
    render(<VaultCanvas graph={graph} view="Circle" selected="skills/research.md" onSelect={vi.fn()} theme="dark" showLinks={false} showLabels />);
    expect(context.quadraticCurveTo).not.toHaveBeenCalled();
    expect(context.fillText).toHaveBeenCalledWith("Research synthesis", expect.any(Number), expect.any(Number));
  });

  it("chooses the nearest file when compact graph hit areas overlap instead of the last drawn file", () => {
    const layout = createLayout(graph, "Links", 240, 420);
    const atlas = layout.nodes.find((node) => node.id === "business/atlas-project.md")!;
    const reading = layout.nodes.find((node) => node.id === "personal/reading.md")!;
    vi.spyOn(layouts, "createLayout").mockReturnValue({ guides: [], nodes: [
      { ...atlas, x: 100, y: 100 }, { ...reading, x: 108, y: 100 },
    ] });
    const onSelect = vi.fn();
    render(<VaultCanvas graph={graph} view="Links" selected={ROOT_FILE} onSelect={onSelect} theme="dark" showLinks={false} showLabels={false} />);
    fireEvent.click(screen.getByRole("img"), { clientX: 100, clientY: 100 });
    expect(onSelect).toHaveBeenLastCalledWith(atlas.id);
    fireEvent.click(screen.getByRole("img"), { clientX: 108, clientY: 100 });
    expect(onSelect).toHaveBeenLastCalledWith(reading.id);
  });

  it("has an explicit fallback without Canvas while retaining the separate document explorer", () => {
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
    render(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={vi.fn()} theme="light" showLinks showLabels={false} />);
    expect(screen.getByRole("status")).toHaveTextContent("Use Jump to file");
    expect(screen.getByRole("button", { name: "Zoom in" })).toBeDisabled();
  });

  it("automatically spins the orbit, respects live reduced motion and cancels animation on unmount", () => {
    const events = new EventTarget();
    const media = { matches: true, addEventListener: events.addEventListener.bind(events), removeEventListener: events.removeEventListener.bind(events) };
    vi.stubGlobal("matchMedia", vi.fn(() => media));
    const frames = vi.spyOn(window, "requestAnimationFrame").mockReturnValue(42);
    const cancel = vi.spyOn(window, "cancelAnimationFrame");
    const { unmount } = render(<VaultCanvas graph={graph} view="3D Orbit" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks showLabels={false} />);
    expect(screen.getByRole("button", { name: "Play orbit" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Rotate orbit right" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Rotate orbit right" }));
    expect(frames).not.toHaveBeenCalled();
    media.matches = false;
    act(() => { events.dispatchEvent(new Event("change")); });
    expect(frames).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Pause orbit" })).toBeEnabled();
    media.matches = true;
    act(() => { events.dispatchEvent(new Event("change")); });
    expect(screen.getByRole("button", { name: "Play orbit" })).toBeDisabled();
    expect(cancel).toHaveBeenCalledWith(42);
    media.matches = false;
    act(() => { events.dispatchEvent(new Event("change")); });
    expect(screen.getByRole("button", { name: "Pause orbit" })).toBeEnabled();
    unmount();
    expect(cancel).toHaveBeenCalledWith(42);
  });

  it("advances directed link particles without React state updates and stops when motion is paused", () => {
    let next: FrameRequestCallback = () => {};
    vi.spyOn(window, "requestAnimationFrame").mockImplementation((callback) => { next = callback; return 9; });
    const cancel = vi.spyOn(window, "cancelAnimationFrame");
    render(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks showLabels={false} />);
    act(() => { next(100); });
    const before = context.arc.mock.calls.slice(-graph.files.length - graph.links.length);
    act(() => { next(164); });
    expect(context.arc.mock.calls.slice(-graph.files.length - graph.links.length)).not.toEqual(before);
    fireEvent.click(screen.getByRole("button", { name: "Pause motion" }));
    expect(screen.getByRole("img")).toHaveAttribute("data-motion", "paused");
    expect(cancel).toHaveBeenCalledWith(9);
    expect(context.closePath).toHaveBeenCalled();
  });

  it("pauses orbital projection independently while the decorative nucleus keeps animating", () => {
    const layout = vi.spyOn(layouts, "createLayout");
    let next: FrameRequestCallback = () => {};
    vi.spyOn(window, "requestAnimationFrame").mockImplementation((callback) => { next = callback; return 10; });
    render(<VaultCanvas graph={graph} view="3D Orbit" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} />);
    act(() => { next(100); next(164); });
    const angle = layout.mock.calls.at(-1)![4]!;
    expect(angle).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole("button", { name: "Pause orbit" }));
    const before = context.arc.mock.calls.slice(-360);
    act(() => { next(200); next(264); });
    expect(layout.mock.calls.at(-1)![4]).toBe(angle);
    expect(context.arc.mock.calls.slice(-360)).not.toEqual(before);
  });

  it("scales actual file dots without resizing the decorative core", () => {
    const { rerender } = render(<VaultCanvas graph={graph} view="Circle" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} dotSize={50} />);
    const node = createLayout(graph, "Circle", 240, 420).nodes.find((item) => item.id === "business/atlas-project.md")!;
    expect(context.arc).toHaveBeenCalledWith(node.x, node.y, node.radius * 0.5 * 0.5, 0, Math.PI * 2);
    context.arc.mockClear();
    rerender(<VaultCanvas graph={graph} view="Circle" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} dotSize={125} />);
    expect(context.arc).toHaveBeenCalledWith(node.x, node.y, node.radius * 1.25 * 0.5, 0, Math.PI * 2);
  });

  it("supports keyboard ring placement and resetting without moving the central brain", () => {
    render(<VaultCanvas graph={graph} view="Ring" selected="skills/research.md" onSelect={vi.fn()} theme="light" showLinks={false} showLabels={false} />);
    const canvas = screen.getByRole("img");
    fireEvent.keyDown(canvas, { key: "ArrowRight" });
    expect(canvas).toHaveAttribute("data-arranged-count", "1");
    expect(screen.getByRole("button", { name: "Reset node positions" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Reset node positions" }));
    expect(canvas).toHaveAttribute("data-arranged-count", "0");
    expect(screen.getByRole("button", { name: "Reset node positions" })).toBeDisabled();
  });

  it("draws six initialed file hubs and frames a selected area while retaining real file counts", () => {
    const { rerender } = render(<VaultCanvas graph={graph} view="Areas" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} />);
    const canvas = screen.getByRole("img");
    expect(canvas).toHaveAttribute("data-node-count", String(graph.files.length));
    expect(canvas).toHaveAttribute("data-area-hub-count", "6");
    for (const initial of ["B", "P", "Y", "C", "S"]) {
      expect(context.fillText).toHaveBeenCalledWith(initial, expect.any(Number), expect.any(Number));
    }
    expect(context.fillText).toHaveBeenCalledWith("BUSINESS 25", expect.any(Number), expect.any(Number));
    rerender(<VaultCanvas graph={graph} view="Areas" selected="business/index.md" onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} />);
    expect(canvas).toHaveAttribute("data-focused-area", "business");
    expect(context.scale.mock.calls.at(-1)![0]).toBeGreaterThan(1);
    expect(context.setLineDash).toHaveBeenCalledWith(expect.arrayContaining([expect.any(Number), expect.any(Number)]));
    fireEvent.click(screen.getByRole("button", { name: "Fit vault" }));
    expect(context.scale).toHaveBeenLastCalledWith(1, 1);
  });

  it("can hide membership guides independently of actual references", () => {
    const { rerender } = render(<VaultCanvas graph={graph} view="Areas" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} showMemberships={false} />);
    expect(context.quadraticCurveTo).not.toHaveBeenCalled();
    expect(context.setLineDash.mock.calls.some(([pattern]) => pattern.length === 2)).toBe(false);
    rerender(<VaultCanvas graph={graph} view="Areas" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} showMemberships />);
    expect(context.setLineDash.mock.calls.some(([pattern]) => pattern.length === 2)).toBe(true);
    context.quadraticCurveTo.mockClear();
    context.setLineDash.mockClear();
    rerender(<VaultCanvas graph={graph} view="Areas" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks showLabels={false} showMemberships={false} />);
    expect(context.quadraticCurveTo).toHaveBeenCalled();
    expect(context.setLineDash.mock.calls.some(([pattern]) => pattern.length === 2)).toBe(false);
  });

  it("frames the destination area rather than stale positions during an animated layout change", () => {
    vi.spyOn(window, "requestAnimationFrame").mockReturnValue(42);
    const { rerender } = render(<VaultCanvas graph={graph} view="Ring" selected="content/index.md" onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} />);
    rerender(<VaultCanvas graph={graph} view="Areas" selected="content/index.md" onSelect={vi.fn()} theme="dark" showLinks={false} showLabels={false} />);
    const members = createLayout(graph, "Areas", 240, 420).nodes.filter((node) => node.file.area === "content");
    const x0 = Math.min(...members.map((node) => node.x - node.radius));
    const x1 = Math.max(...members.map((node) => node.x + node.radius));
    const y0 = Math.min(...members.map((node) => node.y - node.radius));
    const y1 = Math.max(...members.map((node) => node.y + node.radius));
    const center = zoomTransform(screen.getByRole("img")).apply([(x0 + x1) / 2, (y0 + y1) / 2]);
    expect(center[0]).toBeCloseTo(120);
    expect(center[1]).toBeCloseTo(210);
  });

  it("selects on pointer release without requiring a compatibility click and never selects a cancelled touch", () => {
    vi.stubGlobal("PointerEvent", class extends MouseEvent {
      readonly pointerId: number;
      constructor(type: string, init: PointerEventInit) { super(type, init); this.pointerId = init.pointerId ?? 1; }
    });
    const onSelect = vi.fn();
    render(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={onSelect} theme="dark" showLinks={false} showLabels={false} />);
    const canvas = screen.getByRole("img");
    Object.assign(canvas, { setPointerCapture: vi.fn(), hasPointerCapture: () => false });
    const node = createLayout(graph, "Ring", 240, 420).nodes.find((item) => item.id === "skills/research.md")!;
    const event = { pointerId: 1, button: 0, clientX: node.x, clientY: node.y };
    fireEvent.pointerDown(canvas, event);
    fireEvent.pointerUp(canvas, event);
    expect(onSelect).toHaveBeenCalledExactlyOnceWith(node.id);
    fireEvent.click(canvas, event);
    expect(onSelect).toHaveBeenCalledTimes(1);
    fireEvent.pointerDown(canvas, event);
    fireEvent.pointerCancel(canvas, event);
    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it("does not animate a static Timeline and suspends all decorative motion on hidden pages", () => {
    const frames = vi.spyOn(window, "requestAnimationFrame").mockReturnValue(17);
    const cancel = vi.spyOn(window, "cancelAnimationFrame");
    const { rerender } = render(<VaultCanvas graph={graph} view="Timeline" selected={ROOT_FILE} onSelect={vi.fn()} theme="light" showLinks={false} showLabels={false} />);
    expect(frames).not.toHaveBeenCalled();
    rerender(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={vi.fn()} theme="light" showLinks showLabels={false} />);
    expect(frames).toHaveBeenCalledOnce();
    vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    fireEvent(document, new Event("visibilitychange"));
    expect(cancel).toHaveBeenCalledWith(17);
    expect(screen.getByRole("img")).toHaveAttribute("data-motion", "paused");
  });
});
