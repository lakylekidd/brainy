import {
  Activity, Bot, BrainCircuit, Cable, ChevronDown, CircleHelp,
  Command, FileClock, FileText, Fingerprint, Gauge, GitPullRequest,
  Layers3, Menu, Settings2, Wrench, X,
} from "lucide-react";
import { ReactNode, useState } from "react";
import { AgentProfile } from "../api/client";
import { Theme } from "../hooks/useTheme";
import ThemeToggle from "./ThemeToggle";

export type PageId =
  | "dashboard" | "graph" | "agents" | "skills" | "tools" | "connections"
  | "permissions" | "proposals" | "metadata" | "operations" | "audit" | "settings";

const navigation: { id: PageId; label: string; icon: typeof Gauge; group: string }[] = [
  { id: "dashboard", label: "Dashboard", icon: Gauge, group: "Workspace" },
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
  const current = navigation.find((item) => item.id === page)!;
  const initials = profile.id.slice(0, 2).toUpperCase();
  const groups = [...new Set(navigation.map((item) => item.group))];

  function navigate(id: PageId) {
    onNavigate(id);
    setMenuOpen(false);
  }

  return (
    <div className="app-shell">
      {menuOpen && <button className="mobile-scrim" onClick={() => setMenuOpen(false)} aria-label="Close navigation" />}
      <aside className={`sidebar ${menuOpen ? "sidebar-open" : ""}`}>
        <div className="sidebar-brand">
          <span className="brand-mark"><BrainCircuit size={21} /></span>
          <span className="brand-name">homelab <b>brain</b></span>
          <button className="icon-button mobile-close" onClick={() => setMenuOpen(false)} aria-label="Close navigation"><X size={18} /></button>
        </div>
        <div className="workspace-switcher">
          <span className="workspace-avatar">H</span>
          <span className="workspace-label"><b>Home lab</b><small>Personal workspace</small></span>
          <ChevronDown size={15} />
        </div>
        <nav aria-label="Main navigation" className="main-nav">
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
                  {id === "proposals" && <span className="nav-live-dot" />}
                </button>
              ))}
            </div>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="help-card">
            <span className="help-icon"><CircleHelp size={17} /></span>
            <div><b>Need a hand?</b><span>Explore Brain docs</span></div>
          </div>
          <button className="agent-card" onClick={onSignOut} title="Sign out">
            <span className="agent-avatar">{initials}</span>
            <span className="agent-info"><b>{profile.id}</b><small>{profile.teamId ?? "Connected agent"}</small></span>
            <span className="connected-indicator" aria-label="Connected" />
          </button>
        </div>
      </aside>
      <main className="main-area">
        <header className="topbar">
          <div className="topbar-left">
            <button className="icon-button mobile-menu" onClick={() => setMenuOpen(true)} aria-label="Open navigation"><Menu size={20} /></button>
            <div className="breadcrumbs"><span>Brain</span><span className="crumb-slash">/</span><b>{current.label}</b></div>
          </div>
          <div className="topbar-actions">
            <span className="topbar-context"><Command size={14} /> Control plane</span>
            <span className="topbar-divider" />
            <ThemeToggle theme={theme} onToggle={onToggleTheme} />
            <button className="profile-chip" onClick={onSignOut} title="Sign out">
              <span className="agent-avatar avatar-small">{initials}</span>
              <span>{profile.id}</span>
              <ChevronDown size={14} />
            </button>
          </div>
        </header>
        <div className={`page-content${page === "graph" ? " page-content-graph" : ""}`}>
          {children}
        </div>
        <footer className="app-footer">
          <span><span className="status-dot" /> Brain connected</span>
          <span>Homelab Brain <b>•</b> Access is permission-scoped</span>
        </footer>
      </main>
    </div>
  );
}
