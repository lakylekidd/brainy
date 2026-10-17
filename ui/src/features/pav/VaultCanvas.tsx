import { select, zoom, zoomIdentity } from "d3";
import type { ZoomBehavior, ZoomTransform } from "d3";
import { Maximize2, Minus, Pause, Play, Plus, RotateCcw, RotateCw, Undo2 } from "lucide-react";
import { useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import type { Theme } from "../../hooks/useTheme";
import { createLayout, nodePlacement, placeNodes, type Layout, type Placement, type PlotNode, type ViewMode } from "./layouts";
import { flowProgress, linkControl, linkPoint } from "./flow";
import { fileColor, ROOT_FILE, type VaultGraph } from "./vault";

export default function VaultCanvas({ graph, view, selected, onSelect, theme, showLinks, showLabels }: {
  graph: VaultGraph; view: ViewMode; selected: string; onSelect: (id: string) => void;
  theme: Theme; showLinks: boolean; showLabels: boolean;
}) {
  const host = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const context = useRef<CanvasRenderingContext2D | null>(null);
  const camera = useRef<ZoomTransform>(zoomIdentity);
  const behavior = useRef<ZoomBehavior<HTMLCanvasElement, unknown> | null>(null);
  const paintRef = useRef<() => void>(() => {});
  const clock = useRef({ elapsed: 0, rotation: 0 });
  const edits = useRef(new Map<ViewMode, Map<string, Placement>>());
  const drawn = useRef<Layout>({ nodes: [], guides: [] });
  const drag = useRef<{ id: string; pointer: number; startX: number; startY: number; offsetX: number; offsetY: number; moved: boolean } | null>(null);
  const suppressClick = useRef(false);
  const hintId = useId();
  const [size, setSize] = useState({ width: 800, height: 680 });
  const [hovered, setHovered] = useState<string | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  const [playing, setPlaying] = useState(true);
  const [motion, setMotion] = useState(true);
  const [dragging, setDragging] = useState(false);
  const [arranged, setArranged] = useState(0);
  const [pageVisible, setPageVisible] = useState(() => !document.hidden);
  const [reducedMotion, setReducedMotion] = useState(() => window.matchMedia("(prefers-reduced-motion: reduce)").matches);
  const baseLayout = useMemo(() => createLayout(graph, view, size.width, size.height), [graph, view, size]);
  const motionActive = motion && !reducedMotion && pageVisible && !unavailable;

  const paint = useCallback(() => {
    const ctx = context.current;
    if (!ctx || !canvas.current) return;
    const layout = placeNodes(view === "3D Orbit"
      ? createLayout(graph, view, size.width, size.height, clock.current.rotation)
      : baseLayout, edits.current.get(view) ?? new Map(), size.width, size.height);
    drawn.current = layout;
    const positions = new Map(layout.nodes.map((node) => [node.id, node]));
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, size.width, size.height);
    ctx.save();
    ctx.translate(camera.current.x, camera.current.y);
    ctx.scale(camera.current.k, camera.current.k);
    const dark = theme === "dark";
    const ink = dark ? "#e0e9ec" : "#243a40";
    const surface = dark ? "#090c13" : "#f9fafc";
    const muted = dark ? "#8b95aa" : "#657b7c";
    const focus = hovered ?? selected;
    const adjacent = new Set<string>([focus]);
    graph.links.forEach((link) => {
      if (link.source === focus) adjacent.add(link.target);
      if (link.target === focus) adjacent.add(link.source);
    });

    for (const guide of layout.guides) {
      ctx.strokeStyle = dark ? "#454454" : "#cbd5e1";
      ctx.lineWidth = guide.kind === "ellipse" && view === "Ring" ? 1.1 : 0.7;
      ctx.beginPath();
      if (guide.kind === "ellipse") {
        ctx.ellipse(guide.x, guide.y, guide.rx, guide.ry, 0, 0, Math.PI * 2);
        if (view === "Areas") {
          ctx.fillStyle = `${guide.color}0d`;
          ctx.fill();
        }
      } else if (guide.kind === "line") {
        ctx.moveTo(guide.x, guide.y);
        ctx.lineTo(guide.x + guide.rx, guide.y + guide.ry);
      } else if (guide.kind === "path" && guide.points) {
        guide.points.forEach((point, index) => {
          if (index === 0) ctx.moveTo(point.x, point.y);
          else ctx.lineTo(point.x, point.y);
        });
      }
      ctx.stroke();
      if (guide.label) {
        ctx.font = `${view === "Ring" ? "600 " : ""}10px ui-monospace, monospace`;
        const x = guide.x;
        const y = guide.kind === "ellipse" ? guide.y - guide.ry + 11 : guide.y;
        const textWidth = ctx.measureText(guide.label.toUpperCase()).width;
        ctx.fillStyle = surface;
        ctx.fillRect(x - textWidth / 2 - 5, y - 9, textWidth + 10, 13);
        ctx.fillStyle = dark ? guide.color ?? muted : ink;
        ctx.textAlign = "center";
        ctx.fillText(guide.label.toUpperCase(), x, y);
      }
    }

    if (showLinks) {
      for (const [index, link] of graph.links.entries()) {
        const source = positions.get(link.source);
        const target = positions.get(link.target);
        if (!source || !target) continue;
        const active = link.source === focus || link.target === focus;
        ctx.globalAlpha = active ? 0.55 : (view === "Links" ? 0.22 : 0.13);
        ctx.strokeStyle = active ? fileColor(source.file) : muted;
        ctx.lineWidth = active ? 1.2 : 0.7;
        const control = linkControl(source, target);
        ctx.beginPath();
        ctx.moveTo(source.x, source.y);
        ctx.quadraticCurveTo(control.x, control.y, target.x, target.y);
        ctx.stroke();
        const tip = linkPoint(source, target, 0.82);
        const behind = linkPoint(source, target, 0.79);
        const angle = Math.atan2(tip.y - behind.y, tip.x - behind.x);
        ctx.fillStyle = fileColor(source.file);
        ctx.beginPath();
        ctx.moveTo(tip.x, tip.y);
        ctx.lineTo(tip.x - Math.cos(angle - 0.5) * 5, tip.y - Math.sin(angle - 0.5) * 5);
        ctx.lineTo(tip.x - Math.cos(angle + 0.5) * 5, tip.y - Math.sin(angle + 0.5) * 5);
        ctx.closePath();
        ctx.fill();
        if (motionActive) {
          const t = 0.08 + flowProgress(clock.current.elapsed, index) * 0.84;
          const point = linkPoint(source, target, t);
          const tail = linkPoint(source, target, Math.max(0, t - 0.035));
          ctx.globalAlpha = active ? 0.95 : 0.38;
          ctx.strokeStyle = fileColor(source.file);
          ctx.lineWidth = active ? 2 : 1;
          ctx.beginPath();
          ctx.moveTo(tail.x, tail.y);
          ctx.lineTo(point.x, point.y);
          ctx.stroke();
          ctx.beginPath();
          ctx.arc(point.x, point.y, active ? 2.2 : 1.4, 0, Math.PI * 2);
          ctx.fillStyle = dark ? "#f4e8d8" : fileColor(source.file);
          ctx.fill();
        }
      }
      ctx.globalAlpha = 1;
    }

    function label(node: PlotNode, text: string) {
      if (!ctx) return;
      ctx.font = `${node.id === ROOT_FILE ? "600 " : ""}11px 'Segoe UI', sans-serif`;
      ctx.textAlign = "center";
      const width = ctx.measureText(text).width;
      const left = -camera.current.x / camera.current.k;
      const right = (size.width - camera.current.x) / camera.current.k;
      const x = Math.max(left + width / 2 + 6, Math.min(right - width / 2 - 6, node.x));
      const y = node.y + node.radius + 17;
      ctx.fillStyle = surface;
      ctx.fillRect(x - width / 2 - 4, y - 11, width + 8, 16);
      ctx.fillStyle = ink;
      ctx.fillText(text, x, y);
    }

    for (const node of layout.nodes) {
      const active = node.id === selected || node.id === hovered;
      ctx.globalAlpha = view === "3D Orbit" ? 0.4 + node.depth * 0.6 : 1;
      if (focus !== ROOT_FILE && !adjacent.has(node.id)) ctx.globalAlpha *= 0.45;
      const r = node.radius + (active ? 3 : 0);
      ctx.beginPath();
      const sides = node.id === ROOT_FILE || node.file.group === "applications" ? 6 : node.file.group === "skills" ? 4 : 0;
      if (sides) {
        for (let i = 0; i < sides; i++) {
          const angle = i / sides * Math.PI * 2 - Math.PI / 2;
          const x = node.x + Math.cos(angle) * r;
          const y = node.y + Math.sin(angle) * r;
          if (i === 0) ctx.moveTo(x, y);
          else ctx.lineTo(x, y);
        }
        ctx.closePath();
      } else ctx.arc(node.x, node.y, r, 0, Math.PI * 2);
      ctx.fillStyle = fileColor(node.file);
      if (active) {
        ctx.shadowColor = fileColor(node.file);
        ctx.shadowBlur = 20;
      }
      if (node.id === ROOT_FILE || node.file.group === "routines") ctx.fillStyle = surface;
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.strokeStyle = fileColor(node.file);
      ctx.lineWidth = active ? 2 : 1;
      ctx.stroke();
      if (node.file.group === "routines") {
        ctx.beginPath();
        ctx.arc(node.x, node.y, r * 0.5, 0, Math.PI * 2);
        ctx.stroke();
      }
      if (node.id === ROOT_FILE) {
        ctx.fillStyle = fileColor(node.file);
        ctx.font = "bold 12px ui-monospace, monospace";
        ctx.textAlign = "center";
        ctx.fillText("AI", node.x, node.y + 4);
      }
      ctx.globalAlpha = 1;
    }
    // Paint labels last so a later dot cannot obscure the selected document's name.
    layout.nodes.filter((node) => showLabels || node.id === selected || node.id === hovered || node.id === ROOT_FILE)
      .forEach((node) => label(node, node.id === ROOT_FILE ? ROOT_FILE : node.file.title));
    ctx.restore();
  }, [graph, baseLayout, selected, hovered, showLabels, showLinks, size, theme, view, motionActive]);

  useEffect(() => {
    paintRef.current = paint;
    paint();
  }, [paint]);

  useEffect(() => {
    if (!host.current || !canvas.current) return;
    context.current = canvas.current.getContext("2d");
    setUnavailable(!context.current);
    const measure = () => {
      if (!host.current) return;
      const rect = host.current.getBoundingClientRect();
      setSize({ width: Math.max(240, rect.width), height: Math.max(420, rect.height) });
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(host.current);
    return () => { observer.disconnect(); context.current = null; };
  }, []);

  useEffect(() => {
    if (!canvas.current) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.current.width = Math.round(size.width * dpr);
    canvas.current.height = Math.round(size.height * dpr);
    const controller = zoom<HTMLCanvasElement, unknown>()
      .extent([[0, 0], [size.width, size.height]]).scaleExtent([0.45, 4])
      .filter((event) => {
        if (event.type === "wheel") return true;
        if (event.button || event.ctrlKey) return false;
        const point = event.touches?.[0] ?? event;
        const node = hit(point.clientX, point.clientY);
        return event.touches?.length > 1 || !node || (view === "Ring" && node.id === ROOT_FILE);
      })
      .on("zoom", (event) => { camera.current = event.transform; paintRef.current(); });
    const selection = select(canvas.current);
    selection.call(controller).on("dblclick.zoom", null);
    behavior.current = controller;
    selection.call(controller.transform, zoomIdentity);
    return () => { selection.on(".zoom", null); behavior.current = null; };
  }, [size, view]);

  useEffect(() => {
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReducedMotion(media.matches);
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  useEffect(() => {
    const update = () => setPageVisible(!document.hidden);
    document.addEventListener("visibilitychange", update);
    return () => document.removeEventListener("visibilitychange", update);
  }, []);

  useEffect(() => {
    const current = drag.current;
    drag.current = null;
    if (current && canvas.current?.hasPointerCapture(current.pointer)) canvas.current.releasePointerCapture(current.pointer);
    setDragging(false);
    setArranged(edits.current.get(view)?.size ?? 0);
  }, [view, graph, size]);

  useEffect(() => {
    if (!motionActive || !((view === "3D Orbit" && playing) || (showLinks && graph.links.length))) return;
    let frame = 0;
    let last: number | null = null;
    let lastPaint = 0;
    const animate = (now: number) => {
      const delta = last === null ? 0 : Math.min(now - last, 64);
      clock.current.elapsed += delta;
      if (view === "3D Orbit" && playing && !drag.current) clock.current.rotation += delta * 0.000085;
      last = now;
      if (now - lastPaint >= 32) { paintRef.current(); lastPaint = now; }
      frame = requestAnimationFrame(animate);
    };
    frame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(frame);
  }, [playing, motionActive, view, showLinks, graph.links.length]);

  function hit(clientX: number, clientY: number): PlotNode | undefined {
    const rect = canvas.current?.getBoundingClientRect();
    if (!rect) return;
    const [x, y] = camera.current.invert([clientX - rect.left, clientY - rect.top]);
    return [...drawn.current.nodes].reverse().find((node) => Math.hypot(x - node.x, y - node.y) <= node.radius + 7 / camera.current.k);
  }

  function place(id: string, x: number, y: number) {
    const base = view === "3D Orbit" ? createLayout(graph, view, size.width, size.height, clock.current.rotation) : baseLayout;
    const node = base.nodes.find((item) => item.id === id);
    if (!node) return;
    const placement = nodePlacement(node, x, y, size.width, size.height, view);
    if (!placement) return;
    const placements = edits.current.get(view) ?? new Map<string, Placement>();
    placements.set(id, placement);
    edits.current.set(view, placements);
    paintRef.current();
  }

  function startDrag(event: ReactPointerEvent<HTMLCanvasElement>) {
    suppressClick.current = false;
    if (event.button !== 0 || drag.current || unavailable) return;
    const node = hit(event.clientX, event.clientY);
    if (!node || (view === "Ring" && node.id === ROOT_FILE)) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const [x, y] = camera.current.invert([event.clientX - rect.left, event.clientY - rect.top]);
    drag.current = { id: node.id, pointer: event.pointerId, startX: event.clientX, startY: event.clientY, offsetX: node.x - x, offsetY: node.y - y, moved: false };
    event.currentTarget.setPointerCapture(event.pointerId);
    event.preventDefault();
  }

  function movePointer(event: ReactPointerEvent<HTMLCanvasElement>) {
    const current = drag.current;
    if (!current || current.pointer !== event.pointerId) {
      setHovered(hit(event.clientX, event.clientY)?.id ?? null);
      return;
    }
    if (!current.moved && Math.hypot(event.clientX - current.startX, event.clientY - current.startY) < 4) return;
    if (!current.moved) { current.moved = true; setDragging(true); onSelect(current.id); }
    const rect = event.currentTarget.getBoundingClientRect();
    const [x, y] = camera.current.invert([event.clientX - rect.left, event.clientY - rect.top]);
    place(current.id, x + current.offsetX, y + current.offsetY);
  }

  function endDrag(event: ReactPointerEvent<HTMLCanvasElement>) {
    const current = drag.current;
    if (current?.pointer !== event.pointerId) return;
    const tapped = event.type === "pointerup" && !current.moved;
    suppressClick.current = current.moved || tapped;
    drag.current = null;
    if (tapped) onSelect(current.id);
    setDragging(false);
    setArranged(edits.current.get(view)?.size ?? 0);
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
  }

  function changeZoom(factor: number) {
    if (canvas.current && behavior.current) select(canvas.current).call(behavior.current.scaleBy, factor);
  }

  function fit() {
    if (canvas.current && behavior.current) select(canvas.current).call(behavior.current.transform, zoomIdentity);
  }

  function reset() {
    edits.current.delete(view);
    setArranged(0);
    fit();
    paintRef.current();
  }

  function rotate(amount: number) {
    clock.current.rotation += amount;
    paintRef.current();
  }

  return (
    <div className="pav-visual">
      <div className="pav-canvas-host" ref={host}>
        <canvas ref={canvas} role="img" tabIndex={0} aria-describedby={hintId}
          aria-label={`${view} view: ${graph.files.length} real Markdown files and ${graph.links.length} links. Use Jump to file for keyboard inspection, then focus the map and use arrow keys to move the selected file. AGENTS.md stays anchored in Ring view.`}
          data-view={view} data-node-count={graph.files.length} data-link-count={graph.links.length}
          data-motion={reducedMotion ? "reduced" : motionActive ? "running" : "paused"} data-arranged-count={arranged}
          onPointerDown={startDrag} onPointerMove={movePointer} onPointerUp={endDrag} onPointerCancel={endDrag} onLostPointerCapture={endDrag}
          onPointerLeave={() => setHovered(null)}
          onClick={(event) => {
            if (event.defaultPrevented) return;
            if (suppressClick.current) { suppressClick.current = false; return; }
            const node = hit(event.clientX, event.clientY);
            if (node) onSelect(node.id);
          }}
          onKeyDown={(event) => {
            const movements: Record<string, [number, number]> = { ArrowLeft: [-16, 0], ArrowRight: [16, 0], ArrowUp: [0, -16], ArrowDown: [0, 16] };
            const movement = movements[event.key];
            if (!movement || unavailable) return;
            event.preventDefault();
            const node = drawn.current.nodes.find((item) => item.id === selected);
            if (!node) return;
            if (view === "Ring" && node.ring) {
              const angle = node.ring.angle + (movement[0] || movement[1]) / 120;
              place(node.id, size.width / 2 + Math.cos(angle) * node.ring.rx, size.height / 2 + Math.sin(angle) * node.ring.ry);
            } else place(node.id, node.x + movement[0], node.y + movement[1]);
            setArranged(edits.current.get(view)?.size ?? 0);
          }}
          style={{ cursor: dragging ? "grabbing" : hovered ? "move" : "grab" }} />
      </div>
      {unavailable && <p className="pav-canvas-error" role="status">Canvas rendering is unavailable. Use Jump to file and Connected files to explore every document.</p>}
      <div className="pav-map-caption"><span className="pav-live-dot" />{view === "Timeline" ? "Dates from document frontmatter" : "Real files / Directed references, not live runs"}</div>
      <div className="pav-camera" aria-label="Vault camera">
        <button aria-label="Zoom out" onClick={() => changeZoom(0.8)} disabled={unavailable}><Minus size={15} /></button>
        <button aria-label="Zoom in" onClick={() => changeZoom(1.25)} disabled={unavailable}><Plus size={15} /></button>
        <button aria-label="Fit vault" onClick={fit} disabled={unavailable}><Maximize2 size={15} /></button>
        <button aria-label="Reset node positions" onClick={reset} disabled={unavailable || arranged === 0}><Undo2 size={15} /></button>
        <span />
        <button aria-label={motion && !reducedMotion ? "Pause motion" : "Resume motion"} onClick={() => setMotion((value) => !value)}
          disabled={reducedMotion || unavailable} title={reducedMotion ? "Motion is disabled by your reduced-motion preference." : "Pause or resume link flow and orbit"}>
          {motion && !reducedMotion ? <Pause size={15} /> : <Play size={15} />}
        </button>
        {view === "3D Orbit" && <>
          <span />
          <button aria-label="Rotate orbit left" onClick={() => rotate(-0.3)} disabled={unavailable}><RotateCcw size={15} /></button>
          <button aria-label="Rotate orbit right" onClick={() => rotate(0.3)} disabled={unavailable}><RotateCw size={15} /></button>
          <button aria-label={playing && motionActive ? "Pause orbit" : "Play orbit"} onClick={() => setPlaying((value) => !value)}
            disabled={reducedMotion || unavailable || !motion} title={reducedMotion ? "Automatic motion is disabled by your reduced-motion preference." : "Toggle orbit animation"}>
            {playing && motionActive ? <Pause size={15} /> : <Play size={15} />}
          </button>
        </>}
      </div>
      <div className="pav-map-hint" id={hintId}>{hovered ? graph.files.find((file) => file.id === hovered)?.id
        : `${view === "Ring" ? "Drag nodes around their ring" : "Drag nodes to arrange"} / Drag background to pan / Arrow keys move selected file / Scroll to zoom`}</div>
    </div>
  );
}
