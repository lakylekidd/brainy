import { useEffect, useMemo, useRef, useState } from "react";
import type Sigma from "sigma";
import { GraphResult } from "./api";
import { createGraph } from "./model";
import { Theme } from "../../hooks/useTheme";

export default function GraphCanvas({ data, selected, onSelect, theme }: {
  data: GraphResult; selected: string | null; onSelect: (id: string) => void; theme: Theme;
}) {
  const container = useRef<HTMLDivElement>(null);
  const renderer = useRef<Sigma | null>(null);
  const select = useRef(onSelect);
  select.current = onSelect;
  const selectedRef = useRef(selected);
  selectedRef.current = selected;
  const graph = useMemo(() => createGraph(data), [data]);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!container.current || graph.order === 0) return;
    if (typeof WebGL2RenderingContext === "undefined") {
      setError(true);
      return;
    }
    let instance: Sigma | null = null;
    let disposed = false;
    let stopMotion: (() => void) | null = null;
    void Promise.all([import("sigma"), import("sigma/rendering")]).then(([{ default: SigmaRenderer }, { drawDiscNodeHover }]) => {
      if (disposed || !container.current) return;
      instance = new SigmaRenderer(graph, container.current, {
        defaultEdgeType: "arrow", renderEdgeLabels: false,
        labelFont: "Segoe UI, sans-serif", labelSize: 12,
        labelColor: { color: theme === "dark" ? "#dce5ef" : "#17212f" },
        defaultDrawNodeHover: (context, node, settings) =>
          drawDiscNodeHover(context, node, { ...settings, labelColor: { color: "#17212f" } }),
        defaultEdgeColor: theme === "dark" ? "#526078" : "#b6c1d0",
        stagePadding: 40, allowInvalidContainer: false,
        nodeReducer: (id, attributes) => {
          const active = selectedRef.current;
          const connected = active && graph.hasNode(active) && graph.areNeighbors(active, id);
          return {
            ...attributes, size: id === active ? 11 : 7, highlighted: id === active,
            color: !active || id === active || connected ? attributes.color : "#8993a0",
            zIndex: id === active ? 1 : 0,
          };
        },
      });
      renderer.current = instance;
      instance.on("clickNode", ({ node }) => select.current(node));
      const current = instance;
      const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
      const defaults = {
        inertiaDuration: current.getSetting("inertiaDuration"), inertiaRatio: current.getSetting("inertiaRatio"),
        zoomDuration: current.getSetting("zoomDuration"), doubleClickZoomingDuration: current.getSetting("doubleClickZoomingDuration"),
      };
      const applyMotion = () => current.setSettings(motion.matches
        ? { inertiaDuration: 1, inertiaRatio: 0, zoomDuration: 1, doubleClickZoomingDuration: 1 }
        : defaults);
      applyMotion();
      motion.addEventListener("change", applyMotion);
      stopMotion = () => motion.removeEventListener("change", applyMotion);
      setError(false);
    }).catch((cause: unknown) => {
      if (disposed) return;
      console.warn("Graph visualization could not initialize.", cause instanceof Error ? cause.name : "Renderer error");
      setError(true);
    });
    return () => {
      disposed = true;
      stopMotion?.();
      instance?.kill();
      renderer.current = null;
    };
  }, [graph, theme]);

  useEffect(() => {
    const instance = renderer.current;
    if (!instance) return;
    instance.refresh();
  }, [selected, graph, theme]);

  function zoom(factor: number) {
    const camera = renderer.current?.getCamera();
    if (camera) camera.setState({ ratio: camera.getState().ratio * factor });
  }
  return (
    <div className="graph-visual">
      <div className="graph-canvas" ref={container} role="img" aria-label={`Knowledge graph: ${data.entities.length} entities and ${data.relationships.length} relationships. Use Inspect entity for keyboard exploration.`} />
      {error && <p className="graph-render-error" role="status">Graph visualization is unavailable in this browser. Use Inspect entity above to explore.</p>}
      <div className="graph-camera" aria-label="Graph camera">
        <button className="secondary-button" onClick={() => zoom(0.7)} disabled={error} aria-label="Zoom in">+</button>
        <button className="secondary-button" onClick={() => zoom(1.4)} disabled={error} aria-label="Zoom out">-</button>
        <button className="secondary-button" onClick={() => renderer.current?.getCamera().setState({ x: 0.5, y: 0.5, ratio: 1, angle: 0 })} disabled={error}>Fit graph</button>
      </div>
      <span className="graph-canvas-hint">Drag to pan · Scroll to zoom · Select a node to inspect</span>
    </div>
  );
}
