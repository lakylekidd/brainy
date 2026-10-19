import { select, zoom, zoomIdentity } from "d3";
import type { ZoomBehavior, ZoomTransform } from "d3";
import { Maximize2, Minus, Pause, Play, Plus, RotateCcw, RotateCw, Undo2 } from "lucide-react";
import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import type { Theme } from "../../hooks/useTheme";
import { canvasFont, canvasPalette } from "../../appearance";
import { createLayout, nodePlacement, placeNodes, RINGS, type Layout, type Placement, type PlotNode, type RingId, type ViewMode } from "./layouts";
import { drawSignal, flowProgress, signalDuration, signalPath } from "./flow";
import { drawCore, drawFields, drawGround, MAP_MOTION, rippleLayout, transitionLayout } from "./motion";
import { areaLabel, fileColor, ROOT_FILE, type VaultGraph } from "./vault";

function defaultCamera(view: ViewMode, layout: Layout, width: number, height: number): ZoomTransform {
  if (!(view === "Areas" || view === "Links" || view === "3D Orbit") || !layout.nodes.length) return zoomIdentity;
  const points = [
    ...layout.nodes.flatMap((node) => [
      { x: node.x - node.radius, y: node.y - node.radius },
      { x: node.x + node.radius, y: node.y + node.radius },
    ]),
    ...layout.guides.flatMap((guide) => guide.points ?? []),
  ];
  const x0 = Math.min(...points.map((point) => point.x));
  const x1 = Math.max(...points.map((point) => point.x));
  const y0 = Math.min(...points.map((point) => point.y));
  const y1 = Math.max(...points.map((point) => point.y));
  const padding = view === "3D Orbit" ? 48 : 32;
  const scale = Math.max(0.45, Math.min(4,
    (width - padding * 2) / Math.max(1, x1 - x0),
    (height - padding * 2) / Math.max(1, y1 - y0)));
  return zoomIdentity
    .translate(width / 2 - (x0 + x1) / 2 * scale, height / 2 - (y0 + y1) / 2 * scale)
    .scale(scale);
}

export default function VaultCanvas({ graph, view, selected, onSelect, onInspect, onClearSelection, focusedRing, onFocusRing, theme, showLinks, showLabels, showMemberships = true, dotSize = 85 }: {
  graph: VaultGraph; view: ViewMode; selected: string | null; onSelect: (id: string) => void; onClearSelection: () => void;
  focusedRing: RingId | null; onFocusRing: (ring: RingId | null) => void;
  onInspect?: () => void; theme: Theme; showLinks: boolean; showLabels: boolean; showMemberships?: boolean; dotSize?: number;
}) {
  const host = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const context = useRef<CanvasRenderingContext2D | null>(null);
  const camera = useRef<ZoomTransform>(zoomIdentity);
  const behavior = useRef<ZoomBehavior<HTMLCanvasElement, unknown> | null>(null);
  const paintRef = useRef<() => void>(() => {});
  const clock = useRef({ elapsed: 0, sceneElapsed: 0, rotation: 0, sway: 0, velocity: 0 });
  const animatedFocus = useRef<string | null>(null);
  const animatedReset = useRef(false);
  const previousSelected = useRef(selected);
  const previousView = useRef(view);
  const transition = useRef<{ from: Map<string, { x: number; y: number }>; started: number } | null>(null);
  const signals = useRef(new Map<string, { progress: number; elapsed: number }>());
  const arrivals = useRef(new Map<string, number>());
  const emphasis = useRef(new Map<string, { alpha: number; scale: number; elapsed: number }>());
  const visibleLabels = useRef(new Set<string>());
  const ringLabels = useRef(new Map<RingId, HTMLButtonElement>());
  const edits = useRef(new Map<ViewMode, Map<string, Placement>>());
  const drawn = useRef<Layout>({ nodes: [], guides: [] });
  const drag = useRef<{ id: string; pointer: number; startX: number; startY: number; offsetX: number; offsetY: number; moved: boolean } | null>(null);
  const ringPress = useRef<{ ring: RingId; pointer: number; startX: number; startY: number; moved: boolean } | null>(null);
  const backgroundPress = useRef<{ id: string | null; pointer: number; startX: number; startY: number; moved: boolean } | null>(null);
  const suppressClick = useRef(false);
  const hintId = useId();
  const [size, setSize] = useState({ width: 800, height: 680 });
  const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  const [hovered, setHovered] = useState<string | null>(null);
  const [hoveredRing, setHoveredRing] = useState<RingId | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  const [playing, setPlaying] = useState(true);
  const [motion, setMotion] = useState(true);
  const [dragging, setDragging] = useState(false);
  const [arranged, setArranged] = useState(0);
  const [pageVisible, setPageVisible] = useState(() => !document.hidden);
  const [reducedMotion, setReducedMotion] = useState(() => window.matchMedia("(prefers-reduced-motion: reduce)").matches);
  const baseLayout = useMemo(() => createLayout(graph, view, size.width, size.height), [graph, view, size]);
  const motionActive = motion && !reducedMotion && pageVisible && !unavailable;
  const layer = view === "Ring" ? focusedRing : null;
  const layerLabel = RINGS.find((ring) => ring.id === layer)?.label;
  const layerCount = graph.files.filter((file) => file.layer === layer).length;

  const paint = useCallback(() => {
    const ctx = context.current;
    if (!ctx || !canvas.current) return;
    if (previousView.current !== view) {
      transition.current = motionActive ? { from: new Map(drawn.current.nodes.map((node) => [node.id, { x: node.x, y: node.y }])), started: clock.current.elapsed } : null;
      previousView.current = view;
    }
    let layout = placeNodes(view === "3D Orbit"
      ? createLayout(graph, view, size.width, size.height, clock.current.rotation, clock.current.sway)
      : baseLayout, edits.current.get(view) ?? new Map(), size.width, size.height, view);
    if (transition.current) {
      const age = clock.current.elapsed - transition.current.started;
      if (!motionActive || age >= MAP_MOTION.transition + 160) transition.current = null;
      else layout = transitionLayout(layout, transition.current.from, age);
    }
    if (view !== "Timeline" && view !== "3D Orbit") layout = rippleLayout(layout, reducedMotion ? 0 : clock.current.sceneElapsed, size.width, size.height);
    const nodeScale = dotSize / 100 * (size.width <= 760 ? Math.max(0.5, Math.min(0.8, size.width / 600)) : 1);
    layout = { ...layout, nodes: layout.nodes.map((node) => node.id === ROOT_FILE ? node : {
      ...node, radius: node.radius * (node.file.kind === "area" ? dotSize / 100 : nodeScale),
    }) };
    drawn.current = layout;
    const positions = new Map(layout.nodes.map((node) => [node.id, node]));
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, size.width, size.height);
    const dark = theme === "dark";
    if (view !== "Timeline") drawGround(ctx, size.width, size.height, dark);
    ctx.save();
    ctx.translate(camera.current.x, camera.current.y);
    ctx.scale(camera.current.k, camera.current.k);
    const { ink, surface, muted, line } = canvasPalette(theme);
    const focus = layer ? ROOT_FILE : hovered ?? selected ?? ROOT_FILE;
    const focused = !!layer || focus !== ROOT_FILE;
    const focusFile = graph.files.find((file) => file.id === focus);
    const focusArea = focusFile?.kind === "area" ? focusFile.area : null;
    const adjacent = new Set<string>(layer ? graph.files.filter((file) => file.layer === layer).map((file) => file.id) : [focus]);
    if (focusArea) graph.files.filter((file) => file.area === focusArea).forEach((file) => adjacent.add(file.id));
    if (!layer) graph.links.forEach((link) => {
      if (link.source === focus) adjacent.add(link.target);
      if (link.target === focus) adjacent.add(link.source);
    });
    const time = reducedMotion ? 0 : clock.current.elapsed;
    if (view !== "Timeline") drawFields(ctx, layout, time, Math.min(size.width, size.height) * 0.28);

    if (showMemberships && view !== "Circle") {
      ctx.setLineDash([2 / camera.current.k, 4 / camera.current.k]);
      for (const link of graph.memberships) {
        const source = positions.get(link.source);
        const target = positions.get(link.target);
        if (!source || !target) continue;
        ctx.globalAlpha = layer ? adjacent.has(target.id) ? 0.2 : 0.015
          : focusArea && target.file.area === focusArea ? 0.32 : focused ? 0.015 : 0.045;
        ctx.strokeStyle = fileColor(target.file);
        ctx.lineWidth = 0.7 / camera.current.k;
        ctx.beginPath();
        ctx.moveTo(source.x, source.y);
        ctx.lineTo(target.x, target.y);
        ctx.stroke();
      }
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;
    }

    for (const guide of layout.guides) {
      const activeRing = !!layer && guide.ring === layer;
      ctx.strokeStyle = guide.color ? `${guide.color}${activeRing ? "cc" : dark ? "55" : "80"}` : line;
      ctx.lineWidth = activeRing ? 1.8 : guide.kind === "ellipse" && view === "Ring" ? 1.1 : 0.7;
      ctx.setLineDash(view === "Ring" && guide.label?.startsWith("Skills") ? [1, 5] : []);
      ctx.beginPath();
      if (guide.kind === "ellipse") {
        ctx.ellipse(guide.x, guide.y, guide.rx, guide.ry, 0, 0, Math.PI * 2);
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
      ctx.setLineDash([]);
      if (view === "Ring" && guide.label?.startsWith("Applications")) {
        ctx.beginPath();
        for (let i = 0; i < 96; i++) {
          const angle = i / 96 * Math.PI * 2;
          const tick = i % 8 === 0 ? 6 : 3;
          ctx.moveTo(guide.x + Math.cos(angle) * guide.rx, guide.y + Math.sin(angle) * guide.ry);
          ctx.lineTo(guide.x + Math.cos(angle) * (guide.rx - tick), guide.y + Math.sin(angle) * (guide.ry - tick));
        }
        ctx.strokeStyle = `${muted}40`;
        ctx.stroke();
      }
      if (guide.label) {
        ctx.font = `${view === "Ring" ? "600 " : ""}9px ${canvasFont}`;
        const left = -camera.current.x / camera.current.k;
        const right = (size.width - camera.current.x) / camera.current.k;
        const y = guide.kind === "ellipse" ? guide.y - guide.ry + 11 : guide.y;
        const textWidth = ctx.measureText(guide.label.toUpperCase()).width;
        const x = Math.max(left + textWidth / 2 + 6, Math.min(right - textWidth / 2 - 6, guide.x));
        const button = guide.ring && ringLabels.current.get(guide.ring);
        if (button) {
          // Native buttons share the painted label's camera geometry, including its breathing track.
          button.style.left = `${x * camera.current.k + camera.current.x}px`;
          button.style.top = `${(y - 2.5) * camera.current.k + camera.current.y}px`;
          button.style.width = `${textWidth + 10}px`;
          button.style.transform = `translate(-50%, -50%) scale(${camera.current.k})`;
          button.style.color = activeRing ? ink : dark ? guide.color ?? muted : ink;
          button.style.background = surface;
          button.style.visibility = "visible";
          continue;
        }
        ctx.fillStyle = surface;
        ctx.fillRect(x - textWidth / 2 - 5, y - 9, textWidth + 10, 13);
        ctx.fillStyle = dark ? guide.color ?? muted : ink;
        ctx.textAlign = "center";
        ctx.fillText(guide.label.toUpperCase(), x, y);
      }
    }

    const cap = size.width <= 760 ? MAP_MOTION.phoneSignals : MAP_MOTION.ambientSignals;
    const touchesLayer = (source: string, target: string) => adjacent.has(source) || adjacent.has(target);
    const candidates = graph.links.filter((link) => layer ? touchesLayer(link.source, link.target) : !focused || link.source === focus || link.target === focus);
    const stride = Math.max(1, Math.ceil(candidates.length / cap));
    const flowing = new Set(candidates.filter((_, i) => i % stride === 0).slice(0, cap));
    const signalKeys = new Set<string>();
    if (showLinks) {
      for (const [index, link] of graph.links.entries()) {
        const source = positions.get(link.source);
        const target = positions.get(link.target);
        if (!source || !target) continue;
        const active = focused && (layer ? touchesLayer(link.source, link.target)
          : focusArea ? adjacent.has(link.source) && adjacent.has(link.target) : link.source === focus || link.target === focus);
        const depth = view === "3D Orbit" ? Math.min(source.depth, target.depth) * 0.85 + 0.15 : 1;
        ctx.globalAlpha = (active ? 0.65 : focused ? 0.025 : 0.12) * depth;
        ctx.strokeStyle = fileColor(source.file);
        ctx.lineWidth = active ? 1.2 : 0.7;
        const path = signalPath(source, target, view === "Circle" ? { x: size.width / 2, y: size.height / 2 } : undefined);
        const control = path.control;
        ctx.beginPath();
        ctx.moveTo(source.x, source.y);
        ctx.quadraticCurveTo(control.x, control.y, target.x, target.y);
        ctx.stroke();
        const tip = path.at(0.82);
        const behind = path.at(0.79);
        const angle = Math.atan2(tip.y - behind.y, tip.x - behind.x);
        ctx.fillStyle = fileColor(source.file);
        ctx.beginPath();
        ctx.moveTo(tip.x, tip.y);
        ctx.lineTo(tip.x - Math.cos(angle - 0.5) * 5, tip.y - Math.sin(angle - 0.5) * 5);
        ctx.lineTo(tip.x - Math.cos(angle + 0.5) * 5, tip.y - Math.sin(angle + 0.5) * 5);
        ctx.closePath();
        ctx.fill();
        if (!reducedMotion && flowing.has(link)) {
          const key = `${link.source}:${link.target}`;
          signalKeys.add(key);
          const previous = signals.current.get(key);
          const progress = previous ? previous.progress + (time - previous.elapsed) / signalDuration(path.length, camera.current.k) : flowProgress(0, index);
          if (progress >= 1) arrivals.current.set(link.target, time);
          const phase = progress % 1;
          signals.current.set(key, { progress: phase, elapsed: time });
          drawSignal(ctx, path, phase, fileColor(source.file), (focused ? 0.9 : 0.78) * Math.sin(Math.PI * phase) * depth, camera.current.k);
        }
      }
      ctx.globalAlpha = 1;
    }
    for (const key of signals.current.keys()) if (!signalKeys.has(key)) signals.current.delete(key);

    const labelBoxes: { x: number; y: number; width: number; height: number }[] = [];
    function label(node: PlotNode, text: string) {
      if (!ctx) return;
      ctx.font = `${node.id === ROOT_FILE || node.file.kind === "area" ? "600 " : ""}9px ${canvasFont}`;
      ctx.textAlign = "center";
      const width = ctx.measureText(text).width;
      const left = -camera.current.x / camera.current.k;
      const right = (size.width - camera.current.x) / camera.current.k;
      const hub = node.file.kind === "area";
      const labelRight = view !== "Ring" && view !== "Circle" || node.x >= size.width / 2;
      const desiredX = hub ? node.x + (labelRight ? 1 : -1) * (node.radius + 7 + width / 2) : node.x;
      const x = Math.max(left + width / 2 + 6, Math.min(right - width / 2 - 6, desiredX));
      const top = -camera.current.y / camera.current.k;
      const bottom = (size.height - camera.current.y) / camera.current.k;
      const y = Math.max(top + 14, Math.min(bottom - 5, node.y + (hub ? 3 : node.radius + 14)));
      const box = { x: x - width / 2 - 4, y: y - 11, width: width + 8, height: 16 };
      if (node.id !== selected && node.id !== hovered && labelBoxes.some((other) =>
        box.x < other.x + other.width && box.x + box.width > other.x && box.y < other.y + other.height && box.y + box.height > other.y)) return;
      labelBoxes.push(box);
      ctx.globalAlpha = focused && !adjacent.has(node.id) ? 0.25 : 1;
      ctx.fillStyle = surface;
      ctx.fillRect(x - width / 2 - 4, y - 11, width + 8, 16);
      ctx.fillStyle = ink;
      ctx.fillText(text, x, y);
      ctx.globalAlpha = 1;
    }

    for (const node of layout.nodes) {
      const active = !layer && (node.id === selected || node.id === hovered);
      const targetAlpha = layer && node.id === ROOT_FILE ? 1 : focused && !adjacent.has(node.id) ? 0.13 : 1;
      const previous = emphasis.current.get(node.id);
      const delta = previous ? Math.max(0, time - previous.elapsed) : 0;
      const alpha = previous && motionActive ? previous.alpha + (targetAlpha - previous.alpha) * (1 - Math.exp(-delta / 45)) : targetAlpha;
      const scale = previous && motionActive ? previous.scale + ((active ? 1.4 : 1) - previous.scale) * (1 - Math.exp(-delta / 55)) : active ? 1.4 : 1;
      emphasis.current.set(node.id, { alpha, scale, elapsed: time });
      ctx.globalAlpha = (view === "3D Orbit" ? 0.15 + node.depth * 0.85 : 1) * alpha;
      const r = node.radius * scale;
      const arrived = arrivals.current.get(node.id);
      if (arrived !== undefined) {
        const age = time - arrived;
        if (age > 2400) arrivals.current.delete(node.id);
        else if (!reducedMotion && showLinks) {
          const strength = Math.exp(-age / 420);
          ctx.save();
          ctx.globalAlpha *= strength * 0.6;
          ctx.beginPath();
          ctx.arc(node.x, node.y, r + 2 + (1 - strength) * 8, 0, Math.PI * 2);
          ctx.strokeStyle = fileColor(node.file);
          ctx.lineWidth = 1.2;
          ctx.stroke();
          ctx.restore();
        }
      }
      if (node.id === ROOT_FILE && view !== "Timeline") {
        drawCore(ctx, node, time, surface, active);
        ctx.globalAlpha = 1;
        continue;
      }
      ctx.beginPath();
      if (node.file.kind === "area") {
        const color = fileColor(node.file);
        const pulse = motionActive ? Math.sin(time / 1000 * 0.8 + node.x * 0.01) : 0;
        ctx.fillStyle = `${color}12`;
        ctx.beginPath();
        ctx.arc(node.x, node.y, r + 8 + pulse * 2.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = `${color}33`;
        ctx.beginPath();
        ctx.arc(node.x, node.y, r + 3 + pulse * 1.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(node.x, node.y, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "#0a0a0b";
        ctx.font = `700 ${r * 1.1}px ${canvasFont}`;
        ctx.textAlign = "center";
        ctx.fillText(areaLabel(node.file.area)[0], node.x, node.y + r * 0.37);
        ctx.globalAlpha = 1;
        continue;
      }
      const sides = node.id === ROOT_FILE || node.file.kind === "application" ? 6 : node.file.kind === "skill" ? 4 : 0;
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
      if (node.id === ROOT_FILE || node.file.kind === "routine") ctx.fillStyle = surface;
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.strokeStyle = fileColor(node.file);
      ctx.lineWidth = active ? 2 : 1;
      ctx.stroke();
      if (node.file.kind === "routine") {
        ctx.beginPath();
        ctx.arc(node.x, node.y, r * 0.5, 0, Math.PI * 2);
        ctx.stroke();
      }
      if (node.id === ROOT_FILE) {
        ctx.fillStyle = fileColor(node.file);
        ctx.font = `bold 12px ${canvasFont}`;
        ctx.textAlign = "center";
        ctx.fillText("AI", node.x, node.y + 4);
      }
      ctx.globalAlpha = 1;
    }
    // Paint labels last so a later dot cannot obscure the selected document's name.
    layout.nodes.filter((node) => {
      if (node.id === ROOT_FILE && view !== "Timeline") return false;
      const wasVisible = visibleLabels.current.has(node.id);
      const depthVisible = view !== "3D Orbit" || node.depth >= (wasVisible ? 0.4 : 0.55);
      const zoomVisible = camera.current.k >= (wasVisible ? 1.62 : 1.8) && (!focused || adjacent.has(node.id));
      const visible = !layer && (node.id === selected || node.id === hovered)
        || (depthVisible && (node.file.kind === "area" || showLabels || zoomVisible));
      if (visible) visibleLabels.current.add(node.id);
      else visibleLabels.current.delete(node.id);
      return visible;
    }).sort((a, b) => Number(b.file.kind === "area") - Number(a.file.kind === "area")
      || Number(b.id === hovered) - Number(a.id === hovered) || Number(b.id === selected) - Number(a.id === selected)
      || Number(adjacent.has(b.id)) - Number(adjacent.has(a.id)) || a.id.localeCompare(b.id))
      .forEach((node) => label(node, node.file.kind === "area"
        ? `${areaLabel(node.file.area).toUpperCase()} ${graph.files.filter((file) => file.area === node.file.area).length}`
        : node.id === ROOT_FILE ? ROOT_FILE : node.file.title));
    ctx.restore();
  }, [graph, baseLayout, selected, hovered, layer, showLabels, showLinks, showMemberships, size, theme, view, motionActive, reducedMotion, dotSize]);

  useLayoutEffect(() => {
    paintRef.current = paint;
    paint();
  }, [paint]);

  useLayoutEffect(() => {
    if (!host.current || !canvas.current) return;
    context.current = canvas.current.getContext("2d");
    setUnavailable(!context.current);
    const measure = () => {
      if (!host.current) return;
      const rect = host.current.getBoundingClientRect();
      setSize({ width: Math.max(240, rect.width), height: rect.height ? Math.max(240, rect.height) : 420 });
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(host.current);
    return () => { observer.disconnect(); context.current = null; };
  }, []);

  useLayoutEffect(() => {
    if (!canvas.current) return;
    const controller = zoom<HTMLCanvasElement, unknown>()
      .extent([[0, 0], [size.width, size.height]]).scaleExtent([0.45, 4])
      .filter((event) => {
        if (event.type === "wheel") return true;
        if (event.button || event.ctrlKey) return false;
        const point = event.touches?.[0] ?? event;
        const node = hit(point.clientX, point.clientY);
        return event.touches?.length > 1 || (!(hitRing(point.clientX, point.clientY) && !hit(point.clientX, point.clientY, 0))
          && (!node || (view === "Ring" && node.id === ROOT_FILE)));
      })
      .on("zoom", (event) => { camera.current = event.transform; paintRef.current(); });
    const selection = select(canvas.current);
    selection.call(controller).on("dblclick.zoom", null);
    behavior.current = controller;
    selection.call(controller.transform, defaultCamera(view, baseLayout, size.width, size.height));
    return () => { selection.on(".zoom", null); behavior.current = null; };
  }, [baseLayout, size, view]);

  useEffect(() => {
    if (selected === null) {
      setHovered(null);
      setHoveredRing(null);
    }
  }, [selected, focusedRing]);

  useEffect(() => {
    const wasSelected = previousSelected.current !== null;
    previousSelected.current = selected;
    if (!canvas.current || !behavior.current) return;
    if (selected === null && (animatedReset.current || wasSelected)) {
      const selection = select(canvas.current);
      const target = defaultCamera(view, baseLayout, size.width, size.height);
      if (reducedMotion) selection.interrupt("camera-focus").call(behavior.current.transform, target);
      else selection.interrupt("camera-focus").transition("camera-focus").duration(700).call(behavior.current.transform, target);
      animatedReset.current = false;
      return;
    }
    const hub = graph.files.find((file) => file.id === selected && file.kind === "area");
    if (!hub || drag.current?.moved) return;
    const target = view === "3D Orbit"
      ? createLayout(graph, view, size.width, size.height, clock.current.rotation, clock.current.sway) : baseLayout;
    const members = placeNodes(target, edits.current.get(view) ?? new Map(), size.width, size.height, view)
      .nodes.filter((node) => node.file.area === hub.area);
    if (!members.length) return;
    const x0 = Math.min(...members.map((node) => node.x - node.radius));
    const x1 = Math.max(...members.map((node) => node.x + node.radius));
    const y0 = Math.min(...members.map((node) => node.y - node.radius));
    const y1 = Math.max(...members.map((node) => node.y + node.radius));
    const scale = Math.max(1.05, Math.min(2.2, Math.min((size.width - 100) / Math.max(40, x1 - x0), (size.height - 100) / Math.max(40, y1 - y0))));
    const transform = zoomIdentity.translate(size.width / 2 - (x0 + x1) / 2 * scale, size.height / 2 - (y0 + y1) / 2 * scale).scale(scale);
    const selection = select(canvas.current);
    if (animatedFocus.current === selected && !reducedMotion) {
      selection.interrupt("camera-focus").transition("camera-focus").duration(700).call(behavior.current.transform, transform);
    } else {
      selection.interrupt("camera-focus").call(behavior.current.transform, transform);
    }
    animatedFocus.current = null;
  }, [selected, graph, size, view, baseLayout, reducedMotion]);

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
    const current = drag.current ?? ringPress.current ?? backgroundPress.current;
    drag.current = null;
    ringPress.current = null;
    backgroundPress.current = null;
    setHoveredRing(null);
    if (current && canvas.current?.hasPointerCapture(current.pointer)) canvas.current.releasePointerCapture(current.pointer);
    setDragging(false);
    setArranged(edits.current.get(view)?.size ?? 0);
  }, [view, graph, size]);

  useEffect(() => {
    const ids = new Set(graph.files.map((file) => file.id));
    for (const id of emphasis.current.keys()) if (!ids.has(id)) emphasis.current.delete(id);
    for (const id of arrivals.current.keys()) if (!ids.has(id)) arrivals.current.delete(id);
    for (const id of visibleLabels.current) if (!ids.has(id)) visibleLabels.current.delete(id);
  }, [graph]);

  useEffect(() => {
    if (!motionActive || (view === "Timeline" && !transition.current && !(showLinks && graph.links.length))) return;
    let frame = 0;
    let last: number | null = null;
    let lastPaint = 0;
    const animate = (now: number) => {
      const delta = last === null ? 0 : Math.min(now - last, 64);
      clock.current.elapsed += delta;
      if (!drag.current) clock.current.sceneElapsed += delta;
      if (view === "3D Orbit") {
        const target = playing && !drag.current && !hovered && (selected === null || selected === ROOT_FILE) ? MAP_MOTION.orbitSpeed : 0;
        clock.current.velocity = playing ? clock.current.velocity + (target - clock.current.velocity) * (1 - Math.exp(-delta / 500)) : 0;
        if (playing && !drag.current) {
          clock.current.rotation += delta * clock.current.velocity;
          clock.current.sway = Math.sin(clock.current.sceneElapsed / MAP_MOTION.swayPeriod * Math.PI * 2)
            * MAP_MOTION.orbitSway * clock.current.velocity / MAP_MOTION.orbitSpeed;
        }
      }
      last = now;
      if (now - lastPaint >= 32) { paintRef.current(); lastPaint = now; }
      if (view !== "Timeline" || transition.current || (showLinks && graph.links.length)) frame = requestAnimationFrame(animate);
    };
    frame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(frame);
  }, [playing, motionActive, view, showLinks, graph.links.length, hovered, selected]);

  function hitRing(clientX: number, clientY: number): RingId | undefined {
    if (view !== "Ring") return;
    for (const [ring, button] of ringLabels.current) {
      const box = button.getBoundingClientRect();
      if (box.width && clientX >= box.left && clientX <= box.right && clientY >= box.top && clientY <= box.bottom) return ring;
    }
  }

  function hit(clientX: number, clientY: number, padding = 7): PlotNode | undefined {
    const rect = canvas.current?.getBoundingClientRect();
    if (!rect) return;
    const [x, y] = camera.current.invert([clientX - rect.left, clientY - rect.top]);
    let nearest: PlotNode | undefined;
    let distance = Infinity;
    for (const node of [...drawn.current.nodes].reverse()) {
      const separation = Math.hypot(x - node.x, y - node.y);
      if (separation <= node.radius + padding / camera.current.k && separation < distance) {
        nearest = node;
        distance = separation;
      }
    }
    return nearest;
  }

  function place(id: string, x: number, y: number) {
    transition.current = null;
    const base = view === "3D Orbit" ? createLayout(graph, view, size.width, size.height, clock.current.rotation, clock.current.sway) : baseLayout;
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
    if (ringPress.current && ringPress.current.pointer !== event.pointerId) ringPress.current.moved = true;
    if (backgroundPress.current && backgroundPress.current.pointer !== event.pointerId) backgroundPress.current.moved = true;
    if (event.button !== 0 || drag.current || ringPress.current || backgroundPress.current || unavailable) return;
    const actualDot = hit(event.clientX, event.clientY, 0);
    const ring = hitRing(event.clientX, event.clientY);
    if (ring && !actualDot) {
      ringPress.current = { ring, pointer: event.pointerId, startX: event.clientX, startY: event.clientY, moved: false };
      event.currentTarget.setPointerCapture(event.pointerId);
      event.preventDefault();
      return;
    }
    const node = actualDot ?? hit(event.clientX, event.clientY);
    if (!node || (view === "Ring" && node.id === ROOT_FILE)) {
      backgroundPress.current = { id: node?.id ?? null, pointer: event.pointerId, startX: event.clientX, startY: event.clientY, moved: false };
      event.currentTarget.setPointerCapture(event.pointerId);
      return;
    }
    const rect = event.currentTarget.getBoundingClientRect();
    const [x, y] = camera.current.invert([event.clientX - rect.left, event.clientY - rect.top]);
    drag.current = { id: node.id, pointer: event.pointerId, startX: event.clientX, startY: event.clientY, offsetX: node.x - x, offsetY: node.y - y, moved: false };
    event.currentTarget.setPointerCapture(event.pointerId);
    event.preventDefault();
  }

  function movePointer(event: ReactPointerEvent<HTMLCanvasElement>) {
    const background = backgroundPress.current;
    if (background?.pointer === event.pointerId) {
      if (Math.hypot(event.clientX - background.startX, event.clientY - background.startY) >= 4) background.moved = true;
      return;
    }
    const press = ringPress.current;
    if (press?.pointer === event.pointerId) {
      if (Math.hypot(event.clientX - press.startX, event.clientY - press.startY) >= 4) press.moved = true;
      return;
    }
    const current = drag.current;
    if (!current || current.pointer !== event.pointerId) {
      const ring = hitRing(event.clientX, event.clientY);
      const node = hit(event.clientX, event.clientY, 0) ?? (ring ? undefined : hit(event.clientX, event.clientY));
      setHovered(node?.id ?? null);
      setHoveredRing(node ? null : ring ?? null);
      return;
    }
    if (!current.moved && Math.hypot(event.clientX - current.startX, event.clientY - current.startY) < 4) return;
    if (!current.moved) { current.moved = true; setDragging(true); onSelect(current.id); }
    const rect = event.currentTarget.getBoundingClientRect();
    const [x, y] = camera.current.invert([event.clientX - rect.left, event.clientY - rect.top]);
    place(current.id, x + current.offsetX, y + current.offsetY);
  }

  function endDrag(event: ReactPointerEvent<HTMLCanvasElement>) {
    const background = backgroundPress.current;
    if (background?.pointer === event.pointerId) {
      backgroundPress.current = null;
      suppressClick.current = true;
      if (event.type === "pointerup" && !background.moved) {
        if (background.id) activateNode(background.id);
        else clearSelection();
      }
      if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
      return;
    }
    const press = ringPress.current;
    if (press?.pointer === event.pointerId) {
      ringPress.current = null;
      suppressClick.current = true;
      if (event.type === "pointerup" && !press.moved) onFocusRing(layer === press.ring ? null : press.ring);
      if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
      return;
    }
    const current = drag.current;
    if (current?.pointer !== event.pointerId) return;
    const tapped = event.type === "pointerup" && !current.moved;
    suppressClick.current = true;
    drag.current = null;
    if (tapped) activateNode(current.id);
    setDragging(false);
    setArranged(edits.current.get(view)?.size ?? 0);
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
  }

  function clearSelection() {
    setHovered(null);
    setHoveredRing(null);
    animatedReset.current = selected !== null;
    onClearSelection();
  }

  function activateNode(id: string) {
    if (id === selected) clearSelection();
    else { animatedReset.current = false; animatedFocus.current = id; onSelect(id); onInspect?.(); }
  }

  function changeZoom(factor: number) {
    if (canvas.current && behavior.current) select(canvas.current).call(behavior.current.scaleBy, factor);
  }

  function fit() {
    if (canvas.current && behavior.current) {
      select(canvas.current).call(behavior.current.transform, defaultCamera(view, baseLayout, size.width, size.height));
    }
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
        <canvas ref={canvas} width={Math.round(size.width * pixelRatio)} height={Math.round(size.height * pixelRatio)} role="img" tabIndex={0} aria-describedby={hintId}
          aria-label={`${view} view: ${graph.files.length} real Markdown files and ${graph.links.length} links. Use Jump to file for keyboard inspection, then focus the map and use arrow keys to move the selected file. Click the selected dot or empty space, or press Escape, to clear selection. AGENTS.md stays anchored in Ring view.`}
          data-view={view} data-node-count={graph.files.length} data-link-count={graph.links.length}
          data-membership-count={graph.memberships.length} data-area-hub-count={graph.files.filter((file) => file.kind === "area").length}
          data-focused-area={graph.files.find((file) => file.id === selected && file.kind === "area")?.area ?? ""}
          data-focused-ring={layer ?? ""} data-ring-focus-count={layerCount}
          data-selected-file={selected ?? ""}
          data-motion={reducedMotion ? "reduced" : motionActive ? "running" : "paused"} data-arranged-count={arranged}
          onPointerDown={startDrag} onPointerMove={movePointer} onPointerUp={endDrag} onPointerCancel={endDrag} onLostPointerCapture={endDrag}
          onPointerLeave={() => { setHovered(null); setHoveredRing(null); }}
          onClick={(event) => {
            if (event.defaultPrevented) return;
            if (suppressClick.current) { suppressClick.current = false; return; }
            const actualDot = hit(event.clientX, event.clientY, 0);
            const ring = hitRing(event.clientX, event.clientY);
            if (ring && !actualDot) onFocusRing(layer === ring ? null : ring);
            else {
              const node = actualDot ?? hit(event.clientX, event.clientY);
              if (node) activateNode(node.id);
              else clearSelection();
            }
          }}
          onKeyDown={(event) => {
            if (event.key === "Escape") { clearSelection(); event.stopPropagation(); return; }
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
          style={{ cursor: dragging ? "grabbing" : hoveredRing ? "pointer" : hovered ? "move" : "grab" }} />
        {view === "Ring" && !unavailable && <div className="pav-ring-labels" role="group" aria-label="Ring focus">
          {RINGS.filter((ring) => graph.files.some((file) => file.layer === ring.id)).map((ring) => <button key={ring.id}
            ref={(element) => { if (element) ringLabels.current.set(ring.id, element); else ringLabels.current.delete(ring.id); }}
            className="pav-ring-label" type="button" aria-label={`Highlight ${ring.label} ring`} aria-pressed={layer === ring.id}
            data-hovered={hoveredRing === ring.id} aria-describedby={hintId} onClick={() => onFocusRing(layer === ring.id ? null : ring.id)}>
            {ring.label.toUpperCase()} / {graph.files.filter((file) => file.layer === ring.id).length}
          </button>)}
        </div>}
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
      <div className="pav-map-hint" id={hintId} aria-live={layer ? "polite" : "off"}>{layer
        ? `${layerLabel} ring: ${layerCount} visible files highlighted / Click label again or empty space / Escape to clear`
        : hovered ? graph.files.find((file) => file.id === hovered)?.id
          : `${view === "Ring" ? "Click a ring label to highlight its files / Drag nodes around their ring" : "Drag nodes to arrange"} / Click selected dot or background to clear / Drag background to pan / Arrow keys move selected file / Scroll to zoom`}</div>
    </div>
  );
}
