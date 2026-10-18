import {
  Activity, ArrowRight, Boxes, Bot, BrainCircuit,
  Clock3, FileClock, FileText, GitPullRequest, RefreshCw, ShieldAlert,
  Sparkles, Wrench,
} from "lucide-react";
import {
  AuditEvent, DashboardData, ResourceResult, TraceView,
} from "../../api/client";
import { PageId } from "../../components/AppShell";
import type { ReactNode } from "react";
import Today from "../../components/Today";

interface DashboardProps {
  data: DashboardData | null;
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onNavigate: (page: PageId) => void;
  graph?: ReactNode;
}

type MetricProps = {
  label: string;
  value: string;
  note: string;
  icon: typeof Boxes;
  tone: string;
  unavailable?: boolean;
  onClick?: () => void;
};

export default function Dashboard({ data, loading, error, onRefresh, onNavigate, graph }: DashboardProps) {
  const metrics: MetricProps[] = [
    metric("Knowledge documents", "Not available", "No document-count API", FileText, "blue", true),
    metric("Agent memories", "Not available", "No memory-count API", BrainCircuit, "violet", true),
    metric("Skills", count(data?.skills), metricNote(data?.skills), Sparkles, "green", isUnavailable(data?.skills), () => onNavigate("skills")),
    metric("Tools", count(data?.tools), metricNote(data?.tools), Wrench, "amber", isUnavailable(data?.tools), () => onNavigate("tools")),
    metric("Agents", count(data?.agents), cappedNote(data?.agents, 1000), Bot, "blue", isUnavailable(data?.agents), () => onNavigate("agents")),
    metric("Pending proposals", count(data?.proposals), cappedNote(data?.proposals, 100), GitPullRequest, "orange", isUnavailable(data?.proposals), () => onNavigate("proposals")),
    metric("Metadata issues", count(data?.metadata), cappedNote(data?.metadata, 1000), ShieldAlert, "red", isUnavailable(data?.metadata), () => onNavigate("metadata")),
    metric("Recent traces", count(data?.traces), cappedNote(data?.traces, 8), Activity, "violet", isUnavailable(data?.traces), () => onNavigate("operations")),
  ];
  const pending = readyValue(data?.proposals) ?? [];
  const skills = readyValue(data?.skills) ?? [];
  const activity = getRecentActivity(data);
  const apiReady = data?.api.status === "ready" && data.api.value.status === "ready";

  return (
    <div className="dashboard">
      <section className="page-heading">
        <div>
          <div className="eyebrow heading-eyebrow">{new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric", year: "numeric" }).format(new Date()).toUpperCase()} <span className="heading-dot">•</span> SYSTEM OVERVIEW</div>
          <h1>Good to see you.</h1>
          <p>Your Brain, at a glance. Keep knowledge and operations moving.</p>
        </div>
        <div className="heading-actions">
          <div className={`connection-pill ${apiReady ? "connection-good" : ""}`}>
            <span className="status-dot" />
            {apiReady ? "Brain API ready" : "System status unavailable"}
          </div>
          <button className="secondary-button refresh-button" disabled={loading} onClick={onRefresh}>
            <RefreshCw size={15} className={loading ? "spin" : ""} />
            Refresh
          </button>
        </div>
      </section>

      {error && <div className="inline-error" role="alert">{error}</div>}

      <div className="workspace-grid dashboard-workspace">
        <aside className="workspace-left" aria-label="Brain overview">
          <Today />
          <section className="metrics-grid" aria-label="Brain metrics">
            <h2 className="widget-title">Workspace inventory</h2>
            {metrics.map((item) => <MetricCard key={item.label} {...item} />)}
          </section>
          <article className="panel system-panel">
            <PanelHeader eyebrow="PLATFORM HEALTH" title="System pulse" />
            <div className="system-status-row">
              <span className={`system-status-icon ${apiReady ? "pulse-ok" : "pulse-warn"}`}><Activity size={17} /></span>
              <span className="system-status-copy"><b>Brain API</b><small>{apiReady ? "Authenticated / Ready" : data?.api.status === "unavailable" ? data.api.reason : "Checking connection"}</small></span>
              <span className={`status-tag ${apiReady ? "status-healthy" : "status-unknown"}`}>{apiReady ? "Healthy" : "Unknown"}</span>
            </div>
            <div className="system-status-row">
              <span className="system-status-icon pulse-neutral"><Clock3 size={17} /></span>
              <span className="system-status-copy"><b>Background jobs</b><small>Job status is not exposed by the current API</small></span>
              <span className="status-tag status-unknown">Not tracked</span>
            </div>
            <div className="system-status-row">
              <span className="system-status-icon pulse-neutral"><Boxes size={17} /></span>
              <span className="system-status-copy"><b>Connections</b><small>Connection health is not exposed by the current API</small></span>
              <span className="status-tag status-unknown">Not tracked</span>
            </div>
            <div className="system-footer"><button className="text-link" onClick={() => onNavigate("settings")}>System details <ArrowRight size={14} /></button></div>
          </article>
        </aside>
        <div className="workspace-centre">
          {graph}
          <article className="panel registry-panel">
            <PanelHeader eyebrow="CAPABILITIES" title="Skills deck" action="Explore skills" onAction={() => onNavigate("skills")} />
            <div className="registry-counts">
              <div className="registry-count"><Sparkles size={14} /><b>{count(data?.skills)}</b><small>Skills</small></div>
              <div className="registry-count"><Wrench size={14} /><b>{count(data?.tools)}</b><small>Tools</small></div>
            </div>
            <div className="skill-list">
              {skills.slice(0, 3).map((skill) => <div className="skill-row" key={skill.id}>
                <span className="skill-bullet"><Sparkles size={15} /></span>
                <span className="skill-name"><b>{safeLabel(skill.title)}</b><small>{safeLabel(skill.id)}</small></span>
                <span className="registry-active"><span className="status-dot" /> Available</span>
              </div>)}
              {skills.length === 0 && <EmptyState icon={Boxes} message={data?.skills.status === "unavailable" ? data.skills.reason : "No skills are visible to this agent."} />}
            </div>
            <p className="panel-footnote">Registry instructions, not executable actions. Canonical knowledge stays in your vault.</p>
          </article>
        </div>
        <aside className="workspace-right" aria-label="Review and activity">
        <article className="panel review-panel">
          <PanelHeader
            eyebrow="NEEDS YOUR ATTENTION"
            title="Review queue"
            action="All proposals"
            onAction={() => onNavigate("proposals")}
          />
          <div className="review-summary">
            <span className="review-count">{count(data?.proposals)}</span>
            <span><b>pending reviews</b><small>Visible proposals · up to 100 returned</small></span>
            <span className="review-summary-icon"><GitPullRequest size={19} /></span>
          </div>
          <div className="review-list">
            {pending.slice(0, 3).map((proposal) => (
              <div className="review-row" key={proposal.id}>
                <span className="file-icon"><FileText size={15} /></span>
                <span className="review-file">
                  <b title={safeLabel(proposal.path)}>{safeLabel(proposal.path)}</b>
                  <small>{safeLabel(proposal.operation).replaceAll("_", " ")} · {formatAge(proposal.createdAt)}</small>
                </span>
                <span className="status-tag status-pending">Pending</span>
              </div>
            ))}
            {pending.length === 0 && (
              <EmptyState
                icon={GitPullRequest}
                message={data?.proposals.status === "unavailable"
                  ? data.proposals.reason
                  : "You're all caught up. No proposals need review."}
              />
            )}
          </div>
          <button className="text-link panel-link" onClick={() => onNavigate("proposals")}>
            Open proposal queue <ArrowRight size={15} />
          </button>
        </article>
        <article className="panel activity-panel">
          <PanelHeader
            eyebrow="WHAT'S HAPPENING"
            title="Recent activity"
            action="View operations"
            onAction={() => onNavigate("operations")}
          />
          <div className="activity-list">
            {activity.length > 0 ? activity.slice(0, 5).map((item, index) => (
              <div className="activity-row" key={`${item.kind}-${item.timestamp}-${index}`}>
                <span className={`activity-symbol activity-${item.kind}`}>
                  {item.kind === "trace" ? <Activity size={15} /> : <FileClock size={15} />}
                </span>
                <span className="activity-copy">
                  <b>{safeLabel(item.label)}</b>
                  <small>{item.detail}</small>
                </span>
                <time>{formatAge(item.timestamp)}</time>
              </div>
            )) : (
              <EmptyState
                icon={Activity}
                message={data?.audit.status === "unavailable" && data.traces.status === "unavailable"
                  ? "Activity is hidden by your current permissions."
                  : "No recent activity to show."}
              />
            )}
          </div>
          <div className="panel-footnote"><span className="live-mark" /> Activity is permission-scoped to your agent</div>
        </article>
        </aside>
      </div>

      <div className="dashboard-disclaimer">
        <span className="disclaimer-icon"><ShieldAlert size={15} /></span>
        <span><b>Metrics reflect what the current API exposes and your permissions allow.</b> Document and memory totals, connections, and job health need dedicated Brain endpoints.</span>
      </div>
    </div>
  );
}

function metric(
  label: string,
  value: string,
  note: string,
  icon: typeof Boxes,
  tone: string,
  unavailable = false,
  onClick?: () => void,
): MetricProps {
  return { label, value, note, icon, tone, unavailable, onClick };
}

function MetricCard({ label, value, note, icon: Icon, tone, unavailable, onClick }: MetricProps) {
  return (
    <button className={`metric-card ${onClick ? "metric-clickable" : ""}`} onClick={onClick} disabled={!onClick}>
      <span className={`metric-icon metric-${tone}`}><Icon size={18} strokeWidth={1.9} /></span>
      <span className="metric-label">{label}</span>
      <span className={`metric-value ${unavailable ? "metric-muted" : ""}`}>{value}</span>
      <span className="metric-note">{note}</span>
    </button>
  );
}

function PanelHeader({ eyebrow, title, action, onAction }: {
  eyebrow: string; title: string; action?: string; onAction?: () => void;
}) {
  return (
    <div className="panel-header">
      <div><span className="panel-eyebrow">{eyebrow}</span><h2>{title}</h2></div>
      {action && onAction && <button className="text-link panel-action" onClick={onAction}>{action}<ArrowRight size={14} /></button>}
    </div>
  );
}

function EmptyState({ icon: Icon, message }: { icon: typeof Boxes; message: string }) {
  return <div className="empty-state"><Icon size={18} /><span>{message}</span></div>;
}

function count<T>(result: ResourceResult<T[]> | undefined): string {
  return result?.status === "ready" ? result.value.length.toLocaleString() : result ? "—" : "…";
}

function metricNote<T>(result: ResourceResult<T[]> | undefined): string {
  if (!result) return "Loading from Brain API";
  return result.status === "ready" ? "Visible to this agent" : result.reason;
}

function cappedNote<T>(result: ResourceResult<T[]> | undefined, limit: number): string {
  if (!result) return "Loading from Brain API";
  return result.status === "ready" ? `Latest results · up to ${limit}` : result.reason;
}

function isUnavailable<T>(result: ResourceResult<T[]> | undefined): boolean {
  return result?.status !== "ready";
}

function readyValue<T>(result: ResourceResult<T[]> | undefined): T[] | null {
  return result?.status === "ready" ? result.value : null;
}

function getRecentActivity(data: DashboardData | null): {
  kind: "audit" | "trace";
  label: string;
  detail: string;
  timestamp: string;
}[] {
  if (!data) return [];
  const audit = data.audit.status === "ready" ? data.audit.value.map((event: AuditEvent) => ({
    kind: "audit" as const,
    label: safeLabel(event.action),
    detail: `Audit event · ${safeLabel(event.result)}`,
    timestamp: event.timestamp,
  })) : [];
  const traces = data.traces.status === "ready" ? data.traces.value.map((trace: TraceView) => ({
    kind: "trace" as const,
    label: safeLabel(trace.rootAction),
    detail: `Trace · ${safeLabel(trace.status).toLowerCase()}`,
    timestamp: trace.startedAt,
  })) : [];
  return [...audit, ...traces]
    .filter((event) => Number.isFinite(Date.parse(event.timestamp)))
    .sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp));
}

function safeLabel(value: unknown): string {
  return typeof value === "string"
    ? value.replace(/[\u0000-\u001f\u007f]/g, " ").trim().slice(0, 100)
    : "Unknown";
}

function formatAge(value: string): string {
  const timestamp = Date.parse(value);
  if (!Number.isFinite(timestamp)) return "Time unavailable";
  const elapsed = Math.max(0, Date.now() - timestamp);
  const minutes = Math.floor(elapsed / 60_000);
  if (minutes < 1) return "Just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}
