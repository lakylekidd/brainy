import { FormEvent, useEffect, useRef, useState } from "react";
import { Layers3, RefreshCw, Search } from "lucide-react";
import { ApiError, Credentials } from "../../api/client";
import { Theme } from "../../hooks/useTheme";
import GraphCanvas from "./GraphCanvas";
import {
  emptyGraph, entityTypes, EntityType, GraphEntity, GraphResult,
  loadEntity, loadGraph, loadNeighbors, maxEntities, mergeGraph,
} from "./api";
import { typeColors } from "./model";
import { webObsidianDocumentLink } from "./webObsidian";
import "./graph.css";

export default function KnowledgeGraph({ credentials, theme, onUnauthorized, initialFocus }: {
  credentials: Credentials; theme: Theme; onUnauthorized: () => void; initialFocus?: string;
}) {
  const [data, setData] = useState<GraphResult>(emptyGraph);
  const [types, setTypes] = useState<EntityType[]>([...entityTypes]);
  const [draft, setDraft] = useState("");
  const [query, setQuery] = useState("");
  const [focus, setFocus] = useState<string | null>(initialFocus ?? null);
  const [depth, setDepth] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
  const [detail, setDetail] = useState<GraphEntity | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [expanding, setExpanding] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const expansion = useRef<AbortController | null>(null);
  const generation = useRef(0);
  const unauthorized = useRef(onUnauthorized);
  unauthorized.current = onUnauthorized;

  useEffect(() => {
    const controller = new AbortController();
    generation.current++;
    expansion.current?.abort();
    setExpanding(false);
    setData(emptyGraph);
    setSelected(null);
    setError(null);
    if (types.length === 0) { setLoading(false); return () => controller.abort(); }
    setLoading(true);
    const result = focus
      ? loadNeighbors(credentials, focus, types, depth, controller.signal)
      : loadGraph(credentials, types, query, controller.signal);
    result.then((next) => {
      if (!controller.signal.aborted) {
        setData(next);
        if (focus) setSelected(focus);
      }
    }).catch((cause: unknown) => {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && cause.status === 401) {
        setError("Your credentials are no longer valid. Connect again.");
        unauthorized.current();
      }
      else setError(errorMessage(cause));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { controller.abort(); expansion.current?.abort(); };
  }, [credentials, types, query, focus, depth, refresh]);

  useEffect(() => {
    setDetail(null);
    setDetailError(null);
    if (!selected) return;
    const controller = new AbortController();
    loadEntity(credentials, selected, controller.signal).then((entity) => {
      if (!controller.signal.aborted) setDetail(entity);
    }).catch((cause: unknown) => {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && cause.status === 401) unauthorized.current();
      else {
        setDetailError(errorMessage(cause));
        if (cause instanceof ApiError && (cause.status === 403 || cause.status === 404))
          setData((current) => ({
            ...current,
            entities: current.entities.filter((node) => node.id !== selected),
            relationships: current.relationships.filter((edge) => edge.source !== selected && edge.target !== selected),
          }));
      }
    });
    return () => controller.abort();
  }, [credentials, selected]);

  async function expand() {
    if (!selected || loading || expanding) return;
    expansion.current?.abort();
    const controller = new AbortController();
    expansion.current = controller;
    const version = generation.current;
    setExpanding(true);
    setError(null);
    try {
      const next = await loadNeighbors(credentials, selected, types, 1, controller.signal);
      if (!controller.signal.aborted && version === generation.current) setData((current) => mergeGraph(current, next));
    } catch (cause) {
      if (controller.signal.aborted || version !== generation.current) return;
      if (cause instanceof ApiError && cause.status === 401) unauthorized.current();
      else {
        setError(errorMessage(cause));
        if (cause instanceof ApiError && (cause.status === 403 || cause.status === 404)) {
          setData(emptyGraph);
          setSelected(null);
        }
      }
    } finally {
      if (!controller.signal.aborted && version === generation.current) setExpanding(false);
    }
  }

  function search(event: FormEvent) {
    event.preventDefault();
    setFocus(null);
    setQuery(draft.trim());
    setRefresh((value) => value + 1);
  }
  const related = data.relationships.filter((edge) => edge.source === selected || edge.target === selected);
  const labels = new Map(data.entities.map((node) => [node.id, node.label]));
  const documentLink = detail?.id === selected && detail.type === "document"
    ? webObsidianDocumentLink(import.meta.env.VITE_WEB_OBSIDIAN_BASE_URL, detail.source.id)
    : null;
  return (
    <div className="knowledge-graph">
      <section className="page-heading">
        <div><div className="eyebrow heading-eyebrow">EXPLORE YOUR BRAIN</div><h1>Knowledge graph</h1>
          <p>Canonical sources. Deterministic relationships. Only the entities you can access.</p></div>
        <button className="secondary-button" disabled={loading || expanding} onClick={() => setRefresh((value) => value + 1)}>
          <RefreshCw size={15} className={loading ? "spin" : ""} />Refresh graph
        </button>
      </section>
      <section className="panel graph-toolbar" aria-label="Graph controls">
        <form onSubmit={search} className="graph-search">
          <Search size={17} /><label className="sr-only" htmlFor="graph-query">Search graph</label>
          <input id="graph-query" value={draft} maxLength={200} placeholder="Search entities by name or source ID..." onChange={(event) => setDraft(event.target.value)} />
          <button className="secondary-button" type="submit" disabled={types.length === 0}>Search</button>
        </form>
        <fieldset className="graph-filters"><legend>Entity types</legend>
          {entityTypes.map((type) => <label key={type}>
            <input type="checkbox" checked={types.includes(type)} onChange={() => setTypes((current) =>
              current.includes(type) ? current.filter((value) => value !== type) : [...current, type])} />
            <span className="graph-type-dot" style={{ background: typeColors[type] }} />{type}
          </label>)}
        </fieldset>
        <div className="graph-view-controls">
          <span className="graph-mode">{focus ? "Focus graph" : query ? `Search: ${query}` : "Global overview"}</span>
          {focus && <label>Relationship depth <select aria-label="Relationship depth" value={depth} onChange={(event) => setDepth(Number(event.target.value))}>
            <option value={0}>0 — entity only</option><option value={1}>1 — neighbors</option><option value={2}>2 — two hops</option>
          </select></label>}
          {(focus || query) && <button className="secondary-button" onClick={() => { setFocus(null); setQuery(""); setDraft(""); }}>Global graph</button>}
          <span>{data.entities.length} entities · {data.relationships.length} relationships loaded</span>
          <label className="graph-entity-picker">Inspect entity
            <select aria-label="Inspect entity" value={data.entities.some((entity) => entity.id === selected) ? selected ?? "" : ""}
              disabled={loading || data.entities.length === 0}
              onChange={(event) => setSelected(event.target.value || null)}>
              <option value="">Select an entity...</option>
              {data.entities.map((entity) => <option key={entity.id} value={entity.id}>{entity.label} ({entity.type})</option>)}
            </select>
          </label>
        </div>
      </section>
      {error && <div className="inline-error" role="alert">{error} <button className="secondary-button" onClick={() => setRefresh((value) => value + 1)}>Retry graph</button></div>}
      {data.truncated && <p className="graph-notice" role="status">This is a bounded view, not the entire graph. Narrow your filters or search, focus an entity, or expand its neighbors. The browser holds at most 200 entities and 400 relationships.</p>}
      {loading && <p role="status">Loading graph…</p>}
      {!loading && !error && data.entities.length === 0 && <div className="panel graph-empty"><Layers3 size={32} />
        <h2>{types.length === 0 ? "Choose an entity type" : "No accessible entities found"}</h2>
        <p>{types.length === 0 ? "Enable a filter to explore the graph." : "Try another search or filter. Knowledge documents appear after indexing."}</p></div>}
      <div className="graph-layout" aria-busy={loading}>
        <section className="panel graph-explorer" aria-label="Graph exploration">
          {data.entities.length > 0 && <GraphCanvas data={data} selected={selected} onSelect={setSelected} theme={theme} />}
        </section>
        <aside className="panel graph-details" aria-label="Entity details">
          <div className="eyebrow">ENTITY INSPECTOR</div>
          {!selected && <p>Select a graph node or use Inspect entity above to inspect its canonical reference.</p>}
          {selected && !detail && !detailError && <p role="status">Loading entity…</p>}
          {detailError && <p role="alert">{detailError}</p>}
          {detail && detail.id === selected && <>
            <span className="graph-detail-type">{detail.type}</span><h2>{detail.label}</h2>
            <dl><dt>Stable ID</dt><dd>{detail.id}</dd><dt>Source type</dt><dd>{detail.source.kind}</dd>
              <dt>Source record</dt><dd>{detail.source.id}</dd>
              {detail.type === "document" && <><dt>Canonical path</dt><dd>{detail.source.id}</dd><dt>Indexed</dt><dd>{detail.metadata.indexedAt}</dd></>}
              {detail.type === "agent" && <><dt>Enabled</dt><dd>{detail.metadata.enabled === "true" ? "Yes" : "No"}</dd></>}
              {detail.type === "tool" && <><dt>Provider</dt><dd>{detail.metadata.provider}</dd></>}
            </dl>
            {documentLink?.error && <p role="status">{documentLink.error}</p>}
            <div className="graph-detail-actions">
              {documentLink?.href && <a className="secondary-button" href={documentLink.href}
                target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer">
                Open in WebObsidian<span className="sr-only"> (opens in a new tab)</span>
              </a>}
              <button className="primary-button" disabled={loading || expanding || data.entities.length >= maxEntities || types.length === 0} onClick={expand}>
                {expanding ? "Expanding…" : "Expand neighbors"}
              </button>
              <button className="secondary-button" disabled={loading || expanding} onClick={() => { setFocus(detail.id); setQuery(""); setDraft(""); }}>Focus entity</button>
            </div>
            <h3>Loaded relationships ({related.length})</h3>
            {related.length === 0 && <p>No relationships in this view. Expand neighbors to explore.</p>}
            <ul className="graph-relations">{related.map((edge) => {
              const other = edge.source === selected ? edge.target : edge.source;
              return <li key={edge.id}><span>{edge.source === selected ? "Outgoing" : "Incoming"} · {edge.type.replaceAll("_", " ")}</span>
                <button onClick={() => setSelected(other)}>{labels.get(other)}</button></li>;
            })}</ul>
          </>}
        </aside>
      </div>
    </div>
  );
}

function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 403) return "Permission required to explore these graph sources.";
    if (error.status === 404) return "This entity is no longer available or accessible. Refresh the graph.";
    if (error.status === 422) return "A graph source is invalid. Repair its canonical source and retry.";
    if (error.status >= 500) return "A Brain graph source is unavailable. The rest of the application remains usable.";
  }
  return "Could not load graph data. Check your connection and retry.";
}
