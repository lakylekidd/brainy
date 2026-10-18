import {
  Activity, Bot, BrainCircuit, Cable, LogOut,
  FileClock, FileText, Fingerprint, Gauge, GitPullRequest,
  Layers3, Menu, Settings2, Wrench, X,
} from "lucide-react";
import { ReactNode, useEffect, useRef, useState } from "react";
import { AgentProfile } from "../api/client";
import { Theme } from "../hooks/useTheme";
import ThemeToggle from "./ThemeToggle";

export type PageId =
  | "dashboard" | "pav" | "graph" | "agents" | "skills" | "tools" | "connections"
  | "permissions" | "proposals" | "metadata" | "operations" | "audit" | "settings";

const navigation: { id: PageId; label: string; icon: typeof Gauge; group: string }[] = [
  { id: "dashboard", label: "Dashboard", icon: Gauge, group: "Workspace" },
  { id: "pav", label: "PAV OS", icon: BrainCircuit, group: "Workspace" },
  { id: "graph", label: "Knowledge graph", icon: Layers3, group: "Workspace" },
  { id: "agents", label: "Agents", icon: Bot, group: "Control plane" },
  { id: "skills", label: "Skills", icon: BrainCircuit, group: "Control plane" },
  { id: "tools", label: "Tools", icon: Wrench, group: "Control plane" },
  { id: "connections", label: "Connections", icon: Cable, group: "Control plane" },
  { id: "permissions", label: "Permissions", icon: Fingerprint, group: "Control plane" },
  { id: "proposals", label: "Proposals", icon: GitPullRequest, group: "Governance" },
  { id: "metadata", label: "Metadata", icon: FileText, group: "Governance" },
  { id: "operations", label: "Operations", icon: Activity, group: "Governance" },
  { id: "audit", label: "Audit", icon: FileClock, group: "Governance" },
  { id: "settings", label: "Settings", icon: Settings2, group: "System" },
];

interface AppShellProps {
  profile: AgentProfile;
  page: PageId;
  children: ReactNode;
  onNavigate: (page: PageId) => void;
  onSignOut: () => void;
  theme: Theme;
  onToggleTheme: () => void;
}

export default function AppShell({ profile, page, children, onNavigate, onSignOut, theme, onToggleTheme }: AppShellProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLElement>(null);
  const current = navigation.find((item) => item.id === page)!;
  const groups = [...new Set(navigation.map((item) => item.group))];

  useEffect(() => {
    if (menuOpen) menu.current?.querySelector<HTMLButtonElement>('[aria-current="page"]')?.focus();
  }, [menuOpen]);

  function closeMenu() {
    setMenuOpen(false);
    menuButton.current?.focus();
  }

  function navigate(id: PageId) {
    onNavigate(id);
    if (menuOpen) closeMenu();
  }

  return (
    <div className="app-shell">
      <header className="workspace-header">
        <a className="skip-link" href="#workspace-content">Skip to content</a>
        <div className="topbar">
          <div className="topbar-left">
            <button ref={menuButton} className="icon-button mobile-menu" onClick={() => setMenuOpen(!menuOpen)}
              aria-expanded={menuOpen} aria-controls="main-navigation" aria-label={menuOpen ? "Close navigation" : "Open navigation"}>
              {menuOpen ? <X size={18} /> : <Menu size={18} />}
            </button>
            <span className="shell-brand"><BrainCircuit size={16} /><span>HOMELAB <b>BRAIN</b></span></span>
            <span className="shell-context">Shared knowledge / Independent agents</span>
          </div>
          <div className="topbar-actions">
            <span className="profile-chip"><span className="status-dot" /><span>{profile.id}</span></span>
            <ThemeToggle theme={theme} onToggle={onToggleTheme} />
            <button className="icon-button" onClick={onSignOut} title="Sign out" aria-label="Sign out"><LogOut size={15} /></button>
          </div>
        </div>
        <nav ref={menu} id="main-navigation" aria-label="Main navigation" className={`main-nav ${menuOpen ? "main-nav-open" : ""}`}
          onKeyDown={(event) => { if (event.key === "Escape") closeMenu(); }}>
          {groups.map((group) => (
            <div className="nav-group" key={group}>
              <span className="nav-label">{group}</span>
              {navigation.filter((item) => item.group === group).map(({ id, label, icon: Icon }) => (
                <button
                  aria-current={page === id ? "page" : undefined}
                  className={`nav-item ${page === id ? "nav-item-active" : ""}`}
                  key={id}
                  onClick={() => navigate(id)}
                >
                  <Icon size={17} strokeWidth={1.8} />
                  <span>{label}</span>
                </button>
              ))}
            </div>
          ))}
        </nav>
      </header>
      <details className="focus-strip">
        <summary>Focus <span>/ {current.label}</span></summary>
        <div className="focus-cards">
          {([
            ["graph", "01", "Explore knowledge", "Canonical sources and their explicit relationships."],
            ["proposals", "02", "Review proposals", "Knowledge changes remain subject to human approval."],
            ["agents", "03", "Inspect access", "Deterministic grants, resolved by the backend."],
          ] as const).map(([id, number, title, description]) => <button key={id} onClick={() => navigate(id)}>
            <span className="focus-rank">{number}</span><span><b>{title}</b><small>{description}</small></span>
          </button>)}
        </div>
      </details>
      <main className="main-area" id="workspace-content" tabIndex={-1}>
        <div className={`page-content${page === "graph" ? " page-content-graph" : page === "pav" ? " page-content-pav" : ""}`}>
          {children}
        </div>
      </main>
      <footer className="app-footer">
        <span className="footer-label">SYSTEM STATUS</span>
        <span><span className="status-dot" /> Authenticated <b>/</b> {profile.teamId ?? "Independent identity"}</span>
        <span>Access is permission-scoped <b>/</b> Canonical files + Git</span>
      </footer>
    </div>
  );
}
