import { ArrowDownLeft, ArrowUpRight, BrainCircuit, ChevronRight, FileText, Folder, GitBranch, Search, Sparkles } from "lucide-react";
import { useMemo, useState } from "react";
import type { Theme } from "../../hooks/useTheme";
import { VIEWS, type ViewMode } from "./layouts";
import { DemoVaultError, fileColor, filterVault, GROUPS, groupLabel, loadDemoVault, ROOT_FILE } from "./vault";
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
  const [selected, setSelected] = useState(ROOT_FILE);
  const [showLinks, setShowLinks] = useState(true);
  const [showLabels, setShowLabels] = useState(false);
  const visible = useMemo(() => source.graph && filterVault(source.graph, query, area), [source, query, area]);
  const graph = source.graph;

  if (!graph || !visible) return (
    <section className="pav-dashboard"><h1>PAV OS</h1><p role="alert">The demo vault could not be loaded. {source.error}</p></section>
  );

  const file = visible.files.find((item) => item.id === selected) ?? visible.files.find((item) => item.id === ROOT_FILE)!;
  const outgoing = graph.links.filter((link) => link.source === file.id);
  const incoming = graph.links.filter((link) => link.target === file.id);
  const relatedIds = new Set([...outgoing.map((link) => link.target), ...incoming.map((link) => link.source)]);
  const related = graph.files.filter((item) => relatedIds.has(item.id));
  const activeGroups = GROUPS.filter((group) => graph.files.some((item) => item.group === group.id));

  function chooseFile(id: string) {
    if (!visible?.files.some((item) => item.id === id)) { setArea("all"); setQuery(""); }
    setSelected(id);
  }

  return (
    <section className="pav-dashboard" aria-label="PAV OS demo dashboard">
      <header className="pav-heading">
        <div>
          <div className="pav-wordmark"><span><BrainCircuit size={18} /></span>PAV <b>OS</b><i />YOUR PERSONAL OPERATING SYSTEM</div>
          <h1>A map of your mind<span>.</span></h1>
          <p>Ideas, habits and the work that connects them. A little perspective on everything.</p>
        </div>
        <div className="pav-vault-badge"><span className="pav-live-dot" /><div><b>Studio North</b><small>Demo vault / Read only</small></div><Folder size={19} /></div>
      </header>

      <div className="pav-overview">
        <div className="pav-intro"><Sparkles size={17} /><span>A small brain. A connected world.</span></div>
        <div><FileText size={15} /><b>{graph.files.length}</b><span>files</span></div>
        <div><GitBranch size={15} /><b>{graph.links.length}</b><span>links</span></div>
        <div><Folder size={15} /><b>{activeGroups.length}</b><span>areas</span></div>
      </div>

      <div className="pav-workbench">
        <section className="pav-map" aria-label="Demo vault exploration">
          <div className="pav-map-top">
            <div><span className="pav-eyebrow">THE BIG PICTURE</span><h2>Knowledge, connected</h2></div>
            <span className="pav-engine">D3.js <span>+</span> Canvas</span>
          </div>
          <div className="pav-views" role="group" aria-label="Graph views">
            {VIEWS.map((name, index) => <button key={name} onClick={() => setView(name)} aria-pressed={view === name}>
              <span className={`pav-view-symbol pav-view-symbol-${index}`} aria-hidden="true" />{name}
            </button>)}
          </div>
          <div className="pav-toolbar">
            <label className="pav-search"><Search size={15} /><input aria-label="Search demo files" placeholder="Find a thought, a file, a connection..." value={query} onChange={(event) => setQuery(event.target.value)} /></label>
            <select aria-label="Filter demo area" value={area} onChange={(event) => setArea(event.target.value)}>
              <option value="all">All areas</option>
              {activeGroups.map((group) => <option key={group.id} value={group.id}>{group.label}</option>)}
            </select>
          </div>
          <section className="pav-legend" aria-label="Vault areas">
            {activeGroups.map((group) => <button key={group.id} aria-pressed={area === group.id} onClick={() => setArea(area === group.id ? "all" : group.id)}>
              <span className="pav-file-dot" style={{ background: group.color }} /><b>{group.label}</b><span>{graph.files.filter((item) => item.group === group.id).length}</span>
            </button>)}
          </section>
          <div className="pav-kind-legend" aria-label="Node shapes">
            <span><i className="pav-shape-skill" />Skill</span><span><i className="pav-shape-file" />File</span>
            <span><i className="pav-shape-memory" />Memory</span><span><i className="pav-shape-routine" />Routine</span>
            <span><i className="pav-shape-app" />App</span>
          </div>
          <VaultCanvas graph={visible} view={view} selected={file.id} onSelect={chooseFile} theme={theme} showLinks={showLinks} showLabels={showLabels} />
          <div className="pav-map-bottom">
            <span role="status">{visible.files.length === 1 && (query || area !== "all")
              ? "No matching files. AGENTS.md remains as your entry point."
              : `${visible.files.length} of ${graph.files.length} files / ${visible.links.length} visible links`}</span>
            <div><label><input type="checkbox" checked={showLinks} onChange={(event) => setShowLinks(event.target.checked)} />Links</label>
              <label><input type="checkbox" checked={showLabels} onChange={(event) => setShowLabels(event.target.checked)} />Labels</label></div>
          </div>
        </section>

        <aside className="pav-inspector" aria-label="Demo file details">
          <div className="pav-inspector-header"><span className="pav-eyebrow">IN YOUR ORBIT</span><FileText size={17} /></div>
          <label className="pav-jump">Jump to file
            <select aria-label="Jump to file" value={file.id} onChange={(event) => chooseFile(event.target.value)}>
              {graph.files.map((item) => <option key={item.id} value={item.id}>{item.id}</option>)}
            </select>
          </label>
          <div className="pav-file-icon" style={{ color: fileColor(file), borderColor: `${fileColor(file)}55`, background: `${fileColor(file)}12` }}>
            {file.id === ROOT_FILE ? <BrainCircuit size={27} /> : <FileText size={27} />}
          </div>
          <span className="pav-file-type">{file.id === ROOT_FILE ? "THE ENTRY POINT" : `${groupLabel(file.group)} / MARKDOWN`}</span>
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
                <span><b>{item.title}</b><small>{isOutgoing && isIncoming ? "Links both ways" : isOutgoing ? "Links to" : "Linked from"} / {groupLabel(item.group)}</small></span>
                {isOutgoing ? <ArrowUpRight size={13} /> : <ArrowDownLeft size={13} />}
              </button>;
            })}
            {!related.length && <p>No Markdown links to or from this file.</p>}
          </div>
          {file.id !== ROOT_FILE && <button className="pav-back" onClick={() => chooseFile(ROOT_FILE)}><BrainCircuit size={14} />Back to AGENTS.md<ChevronRight size={14} /></button>}
        </aside>
      </div>

      <footer className="pav-footnote"><span>{VIEW_NOTES[view]}</span><span>Fictional workspace. Real Markdown files. No live Brain data.</span></footer>
    </section>
  );
}
