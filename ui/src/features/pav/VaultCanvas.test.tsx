import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createLayout } from "./layouts";
import { loadDemoVault, ROOT_FILE } from "./vault";
import VaultCanvas from "./VaultCanvas";

const graph = loadDemoVault();
const context = {
  setTransform: vi.fn(), clearRect: vi.fn(), save: vi.fn(), restore: vi.fn(), translate: vi.fn(), scale: vi.fn(),
  beginPath: vi.fn(), closePath: vi.fn(), ellipse: vi.fn(), fill: vi.fn(), moveTo: vi.fn(), lineTo: vi.fn(), stroke: vi.fn(),
  fillRect: vi.fn(), fillText: vi.fn(), quadraticCurveTo: vi.fn(), arc: vi.fn(),
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
    const canvas = screen.getByRole("img", { name: /^Ring view: 30 real Markdown files/ });
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
    render(<VaultCanvas graph={graph} view="Circle" selected={ROOT_FILE} onSelect={vi.fn()} theme="dark" showLinks={false} showLabels />);
    expect(context.quadraticCurveTo).not.toHaveBeenCalled();
    expect(context.fillText).toHaveBeenCalledWith("Research synthesis", expect.any(Number), expect.any(Number));
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

  it("does not start frame loops when links are hidden outside Orbit and suspends hidden pages", () => {
    const frames = vi.spyOn(window, "requestAnimationFrame").mockReturnValue(17);
    const cancel = vi.spyOn(window, "cancelAnimationFrame");
    const { rerender } = render(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={vi.fn()} theme="light" showLinks={false} showLabels={false} />);
    expect(frames).not.toHaveBeenCalled();
    rerender(<VaultCanvas graph={graph} view="Ring" selected={ROOT_FILE} onSelect={vi.fn()} theme="light" showLinks showLabels={false} />);
    expect(frames).toHaveBeenCalledOnce();
    vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    fireEvent(document, new Event("visibilitychange"));
    expect(cancel).toHaveBeenCalledWith(17);
    expect(screen.getByRole("img")).toHaveAttribute("data-motion", "paused");
  });
});
