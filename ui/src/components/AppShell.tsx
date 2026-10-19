import {
  Activity, Bot, BrainCircuit, Cable, LogOut,
  FileClock, FileText, Fingerprint, Gauge, GitPullRequest,
  Menu, Settings2, Wrench, X,
} from "lucide-react";
import { ReactNode, useEffect, useRef, useState } from "react";
import { AgentProfile } from "../api/client";
import { Theme } from "../hooks/useTheme";
import ThemeToggle from "./ThemeToggle";

export type PageId =
  | "pav" | "agents" | "skills" | "tools" | "connections"
  | "permissions" | "proposals" | "metadata" | "operations" | "audit" | "settings";

const navigation: { id: PageId; label: string; icon: typeof Gauge; group: string }[] = [
  { id: "pav", label: "Dashboard", icon: BrainCircuit, group: "Workspace" },
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
const navigationColumns = [["Workspace", "Governance"], ["Control plane", "System"]] as const;

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

  useEffect(() => {
    if (menuOpen) menu.current?.querySelector<HTMLButtonElement>('[aria-current="page"]')?.focus();
  }, [menuOpen]);

  useEffect(() => {
    if (!menuOpen) return;
    function closeOnOutsideClick(event: MouseEvent) {
      const target = event.target as Node;
      if (!menu.current?.contains(target) && !menuButton.current?.contains(target)) closeMenu();
    }
    document.addEventListener("mousedown", closeOnOutsideClick);
    return () => document.removeEventListener("mousedown", closeOnOutsideClick);
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
            <span className="shell-brand"><BrainCircuit size={16} /><span>BRAINY</span></span>
            <span className="shell-context">Shared knowledge / Independent agents</span>
            <button ref={menuButton} className="navigation-trigger" onClick={() => setMenuOpen(!menuOpen)}
              aria-expanded={menuOpen} aria-controls="main-navigation" aria-label={menuOpen ? "Close navigation" : "Open navigation"}>
              <span className={`navigation-icon${menuOpen ? " navigation-icon-open" : ""}`} aria-hidden="true">
                <Menu className="navigation-icon-menu" size={14} /><X className="navigation-icon-close" size={14} />
              </span><span>Navigate</span>
            </button>
          </div>
          <div className="topbar-actions">
            <span className="profile-chip"><span className="status-dot" /><span>{profile.id}</span></span>
            <ThemeToggle theme={theme} onToggle={onToggleTheme} />
            <button className="icon-button" onClick={onSignOut} title="Sign out" aria-label="Sign out"><LogOut size={15} /></button>
          </div>
        </div>
        <nav ref={menu} id="main-navigation" aria-label="Main navigation" className={`main-nav ${menuOpen ? "main-nav-open" : ""}`}
          onKeyDown={(event) => { if (event.key === "Escape") closeMenu(); }}>
          {navigationColumns.map((column, index) => (
            <div className="nav-column" key={index}>
              {column.map((group) => (
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
            </div>
          ))}
        </nav>
      </header>
      <main className="main-area" id="workspace-content" tabIndex={-1}>
        <div className={`page-content${page === "pav" ? " page-content-pav" : " page-content-constrained"}${page === "permissions" ? " page-content-permissions" : ""}`}>
          {children}
        </div>
      </main>
      {page !== "pav" && <footer className="workspace-footer">
        <div className="workspace-footer-content">© {new Date().getFullYear()} Brainy contributors · PolyForm Noncommercial License 1.0.0</div>
      </footer>}
    </div>
  );
}
