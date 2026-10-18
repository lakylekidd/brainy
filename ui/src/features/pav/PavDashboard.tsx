import { ArrowDownLeft, ArrowUpRight, BrainCircuit, ChevronRight, FileText, Folder, GitBranch, Maximize, Minimize, Search, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import type { Theme } from "../../hooks/useTheme";
import Today from "../../components/Today";
import { VIEWS, type RingId, type ViewMode } from "./layouts";
import { AREAS, DemoVaultError, FILE_KINDS, KIND_LABELS, areaLabel, fileColor, filterVault, loadDemoVault, ROOT_FILE } from "./vault";
import VaultCanvas from "./VaultCanvas";
import "./pav.css";

const VIEW_NOTES: Record<ViewMode, string> = {
  Ring: "Skills, knowledge, memory, routines, applications. One connected brain.",
  Circle: "One shared circle. Follow the threads between your ideas.",
  Areas: "Step back and see the neighborhoods of your workspace.",
  Links: "Let the connections reveal the shape of your knowledge.",
  Timeline: "Watch your workspace take shape, one document at a time.",
  "3D Orbit": "A new perspective on the same connected brain.",
};

export default function PavDashboard({ theme }: { theme: Theme }) {
  const [source] = useState(() => {
    try { return { graph: loadDemoVault(), error: null }; }
    catch (error) {
      if (!(error instanceof DemoVaultError)) throw error;
      return { graph: null, error: error.message };
    }
  });
  const [view, setView] = useState<ViewMode>("Ring");
  const [query, setQuery] = useState("");
  const [area, setArea] = useState("all");
  const [kind, setKind] = useState("all");
  const [selected, setSelected] = useState<string | null>(null);
  const [focusedRing, setFocusedRing] = useState<RingId | null>(null);
  const [showLinks, setShowLinks] = useState(true);
  const [showLabels, setShowLabels] = useState(false);
  const [showMemberships, setShowMemberships] = useState(true);
  const [dotSize, setDotSize] = useState(85);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);
  const [fullscreenError, setFullscreenError] = useState("");
  const map = useRef<HTMLElement>(null);
  const detailsButton = useRef<HTMLButtonElement>(null);
  const visible = useMemo(() => source.graph && filterVault(source.graph, query, area, kind), [source, query, area, kind]);
  const graph = source.graph;

  useEffect(() => {
    const update = () => setFullscreen(document.fullscreenElement === map.current);
    document.addEventListener("fullscreenchange", update);
    return () => document.removeEventListener("fullscreenchange", update);
  }, []);

  async function toggleFullscreen() {
    setFullscreenError("");
    if (!map.current?.requestFullscreen) {
      setFullscreenError("Fullscreen is unavailable in this browser. You can continue exploring the map.");
      return;
    }
    try {
      if (document.fullscreenElement === map.current) await document.exitFullscreen();
      else await map.current.requestFullscreen();
    } catch (error) {
      console.warn("PAV map fullscreen request failed.", error);
      setFullscreenError("The browser could not change fullscreen mode. You can continue exploring the map.");
    }
  }

  if (!graph || !visible) return (
    <section className="pav-dashboard"><h1>PAV OS</h1><p role="alert">The demo vault could not be loaded. {source.error}</p></section>
  );

  const selectedFile = visible.files.find((item) => item.id === selected);
  const file = selectedFile ?? visible.files.find((item) => item.id === ROOT_FILE)!;
  const outgoing = graph.links.filter((link) => link.source === file.id);
  const incoming = graph.links.filter((link) => link.target === file.id);
  const relatedIds = new Set([...outgoing.map((link) => link.target), ...incoming.map((link) => link.source)]);
  const related = graph.files.filter((item) => relatedIds.has(item.id));
  const activeGroups = AREAS.filter((area) => graph.files.some((item) => item.area === area.id));

  function chooseFile(id: string, inspect = true) {
    if (!visible?.files.some((item) => item.id === id)) { setArea("all"); setQuery(""); setKind("all"); }
    setSelected(id);
    setFocusedRing(null);
    setDetailsOpen(inspect);
  }

  function focusRing(ring: RingId | null) {
    setFocusedRing(ring);
    setSelected(null);
    setDetailsOpen(false);
  }

  function clearSelection() {
    setSelected(null);
    setFocusedRing(null);
    setDetailsOpen(false);
  }

  function closeDetails() {
    setDetailsOpen(false);
    detailsButton.current?.focus({ preventScroll: true });
  }

  return (
    <section className="pav-dashboard" aria-label="PAV OS demo dashboard">
      <div className="workspace-grid">
        <aside className="workspace-left" aria-label="Demo vault overview">
          <Today />
          <section className="pav-inventory">
            <h2 className="widget-title">Vault inventory</h2>
            <p>Studio North / Public example files</p>
            <div className="pav-overview">
              <div><FileText size={13} /><span>Markdown files</span><b>{graph.files.length}</b></div>
              <div><GitBranch size={13} /><span>Directed references</span><b>{graph.links.length}</b></div>
              <div><Folder size={13} /><span>Workspace areas</span><b>{activeGroups.length}</b></div>
            </div>
          </section>
          <section className="pav-demo-widget">
            <h2 className="widget-title">The entry point</h2>
            <p><b>AGENTS.md</b> is the master map. It tells an agent where knowledge, skills, memory and routines live.</p>
            <button className="text-link" onClick={() => chooseFile(ROOT_FILE)}>Read AGENTS.md <ArrowUpRight size={12} /></button>
          </section>
          <p className="pav-rail-note">Every ordinary dot is a real Markdown file. Trails show reference direction, not live agent activity.</p>
        </aside>
        <div className="workspace-centre">
      <header className="pav-heading">
        <h1><BrainCircuit size={17} />PAV OS <em>Agentic OS</em></h1>
        <p>Studio North <span>/</span> Demo vault / Read only</p>
      </header>

      <div className="pav-workbench">
        <section className="pav-map" aria-label="Demo vault exploration" ref={map}
          onKeyDown={(event) => {
            if (event.key !== "Escape") return;
            clearSelection();
            if (detailsOpen) detailsButton.current?.focus({ preventScroll: true });
          }}>
          <div className="pav-map-top">
            <div className="pav-views" role="group" aria-label="Graph views">
              {VIEWS.map((name) => <button key={name} onClick={() => { setView(name); setFocusedRing(null); }} aria-pressed={view === name}>{name}</button>)}
            </div>
            <div className="pav-map-actions"><span className="pav-engine">D3.js <span>+</span> Canvas</span>
              <button className="pav-fullscreen" onClick={toggleFullscreen} aria-label={fullscreen ? "Exit map fullscreen" : "Enter map fullscreen"} aria-pressed={fullscreen}>
                {fullscreen ? <Minimize size={15} /> : <Maximize size={15} />}<span>{fullscreen ? "Exit fullscreen" : "Fullscreen"}</span>
              </button>
            </div>
          </div>
          <div className="pav-toolbar">
            <label className="pav-search"><Search size={15} /><input aria-label="Search demo files" placeholder="Find a thought, a file, a connection..." value={query} onChange={(event) => { setQuery(event.target.value); setFocusedRing(null); }} /></label>
            <select aria-label="Filter demo area" value={area} onChange={(event) => { setArea(event.target.value); setFocusedRing(null); }}>
              <option value="all">All areas</option>
              {activeGroups.map((group) => <option key={group.id} value={group.id}>{group.label}</option>)}
            </select>
            <select aria-label="Filter file type" value={kind} onChange={(event) => { setKind(event.target.value); setFocusedRing(null); }}>
              <option value="all">All file types</option>
              {FILE_KINDS.map((value) => <option key={value} value={value}>{KIND_LABELS[value]}</option>)}
            </select>
            <label className="pav-jump"><span>Jump to file</span>
              <select aria-label="Jump to file" value={file.id} onChange={(event) => chooseFile(event.target.value)}>
                {graph.files.map((item) => <option key={item.id} value={item.id}>{item.id}</option>)}
              </select>
            </label>
            <button className="pav-details-toggle" ref={detailsButton} aria-expanded={detailsOpen} aria-controls="pav-file-details" onClick={() => setDetailsOpen((value) => !value)}>
              <FileText size={15} />File details
            </button>
          </div>
          {fullscreenError && <p className="pav-fullscreen-error" role="alert">{fullscreenError}</p>}
          <section className="pav-legend" aria-label="Vault areas">
            {activeGroups.map((group) => <button key={group.id} aria-pressed={area === group.id} onClick={() => { setArea(area === group.id ? "all" : group.id); setFocusedRing(null); }}>
              <span className="pav-file-dot" style={{ background: group.color }} /><b>{group.label}</b><span>{graph.files.filter((item) => item.area === group.id).length}</span>
            </button>)}
          </section>
          <div className="pav-kind-legend" aria-label="Node shapes">
            <span><i className="pav-shape-area">A</i>Area overview</span>
            <span><i className="pav-shape-skill" />Skill</span><span><i className="pav-shape-file" />File</span>
            <span><i className="pav-shape-memory" />Memory</span><span><i className="pav-shape-routine" />Routine</span>
            <span><i className="pav-shape-app" />App</span>
          </div>
          <VaultCanvas graph={visible} view={view} selected={selectedFile?.id ?? null} onSelect={(id) => chooseFile(id, false)} onInspect={() => setDetailsOpen(true)}
            onClearSelection={clearSelection}
            focusedRing={focusedRing} onFocusRing={focusRing} theme={theme} showLinks={showLinks} showLabels={showLabels} showMemberships={showMemberships} dotSize={dotSize} />
          <div className="pav-map-bottom">
            <span role="status">{visible.files.length === 1 && (query || area !== "all" || kind !== "all")
              ? "No matching files. AGENTS.md remains as your entry point."
              : `${visible.files.length} of ${graph.files.length} files / ${visible.links.length} visible links`}</span>
            <div><label className="pav-dot-size">Dot size <input type="range" aria-label="Dot size" aria-valuetext={`${dotSize}%`} min="50" max="125" step="5" value={dotSize} onChange={(event) => setDotSize(Number(event.target.value))} /><output aria-hidden="true">{dotSize}%</output></label>
              <label><input type="checkbox" checked={showLinks} onChange={(event) => setShowLinks(event.target.checked)} />Links</label>
              <label title="Dashed area-membership guides, not Markdown references"><input type="checkbox" checked={showMemberships} onChange={(event) => setShowMemberships(event.target.checked)} />Membership</label>
              <label><input type="checkbox" checked={showLabels} onChange={(event) => setShowLabels(event.target.checked)} />Labels</label></div>
          </div>
          <aside className="pav-inspector" id="pav-file-details" aria-label="Demo file details" hidden={!detailsOpen}>
            <div className="pav-inspector-header"><span className="pav-eyebrow">SELECTED FILE</span>
              <button aria-label="Close file details" onClick={closeDetails}><X size={17} /></button>
            </div>
            <div className="pav-file-icon" style={{ color: fileColor(file), borderColor: `${fileColor(file)}55`, background: `${fileColor(file)}12` }}>
              {file.id === ROOT_FILE ? <BrainCircuit size={27} /> : <FileText size={27} />}
            </div>
            <span className="pav-file-type">{file.id === ROOT_FILE ? "THE ENTRY POINT" : `${areaLabel(file.area)} / ${KIND_LABELS[file.kind]} / MARKDOWN`}</span>
            {file.kind === "area" && <p className="pav-area-summary">{graph.files.filter((item) => item.area === file.area).length} real files in this area, including this overview.
              Dashed spokes show membership; animated links show actual Markdown references.</p>}
            <h2>{file.title}</h2>
            <code className="pav-file-path">{file.id}</code>
            <p className="pav-file-summary">{file.body.split(/\r?\n\r?\n/).find((paragraph) => !paragraph.startsWith("#"))?.replace(/\s+/g, " ")}</p>
            <div className="pav-file-meta"><span>Created</span><time dateTime={file.created}>{file.created}</time></div>
            <div className="pav-file-meta"><span>Source</span><span>ui/demo-vault</span></div>
            <details className="pav-document" key={file.id}><summary>Read document <FileText size={13} /></summary><pre>{file.body}</pre></details>
            <div className="pav-connections-title"><h3>Connected files</h3><span>{related.length}</span></div>
            <div className="pav-connections">
              {related.map((item) => {
                const isOutgoing = outgoing.some((link) => link.target === item.id);
                const isIncoming = incoming.some((link) => link.source === item.id);
                return <button key={item.id} onClick={() => chooseFile(item.id)}>
                  <span className="pav-file-dot" style={{ background: fileColor(item) }} />
                  <span><b>{item.title}</b><small>{isOutgoing && isIncoming ? "Links both ways" : isOutgoing ? "Links to" : "Linked from"} / {areaLabel(item.area)}</small></span>
                  {isOutgoing ? <ArrowUpRight size={13} /> : <ArrowDownLeft size={13} />}
                </button>;
              })}
              {!related.length && <p>No Markdown links to or from this file.</p>}
            </div>
            {file.kind === "area" && <section className="pav-area-members" aria-label="Files in this area">
              <h3>Area members</h3>
              <div className="pav-connections">
                {graph.files.filter((item) => item.area === file.area && item.id !== file.id).map((item) => <button key={item.id} onClick={() => chooseFile(item.id)}>
                  <span className="pav-file-dot" style={{ background: fileColor(item) }} />
                  <span><b>{item.title}</b><small>{KIND_LABELS[item.kind]} / {item.id}</small></span><ArrowUpRight size={13} />
                </button>)}
              </div>
            </section>}
            {file.id !== ROOT_FILE && <button className="pav-back" onClick={() => chooseFile(ROOT_FILE)}><BrainCircuit size={14} />Back to AGENTS.md<ChevronRight size={14} /></button>}
          </aside>
        </section>
      </div>
      <footer className="pav-footnote"><span>{VIEW_NOTES[view]}</span><span>Fictional workspace. Real Markdown files. No live Brain data.</span></footer>
      <section className="pav-demo-widget">
        <h2 className="widget-title">Skills deck <small>Example instructions / Read only</small></h2>
        <div className="pav-skill-deck">
          {graph.files.filter((item) => item.kind === "skill").map((item) => <button key={item.id} onClick={() => chooseFile(item.id)}>
            <FileText size={16} /><span><b>{item.title}</b><small>{item.id}</small></span><ArrowUpRight size={13} />
          </button>)}
        </div>
      </section>
        </div>
        <aside className="workspace-right" aria-label="Demo routines and context">
          <section className="pav-demo-widget">
            <h2 className="widget-title">Routines <small>Example documents / Not scheduled</small></h2>
            <p>Repeatable workflows that connect notes to skills. Select one to inspect its real source and references.</p>
            <div className="pav-rail-files">
              {graph.files.filter((item) => item.kind === "routine").map((item) => <button key={item.id} onClick={() => chooseFile(item.id)}>
                <span className="pav-file-dot" style={{ background: fileColor(item) }} />{item.title}<ArrowUpRight size={12} />
              </button>)}
            </div>
          </section>
          <section className="pav-demo-widget">
            <h2 className="widget-title">Working context <small>Example memory files / Not database memories</small></h2>
            <div className="pav-rail-files">
              {graph.files.filter((item) => item.kind === "memory").map((item) => <button key={item.id} onClick={() => chooseFile(item.id)}>
                <span className="pav-file-dot" style={{ background: fileColor(item) }} />{item.title}<ArrowUpRight size={12} />
              </button>)}
            </div>
          </section>
          <section className="pav-demo-widget">
            <h2 className="widget-title">Local files, not live systems</h2>
            <p>This graph explores the bundled demo vault only. No calendar, mail, execution runner or external application is connected.</p>
          </section>
        </aside>
      </div>
    </section>
  );
}
