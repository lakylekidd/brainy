import { useEffect, useRef, useState } from "react";
import { X } from "lucide-react";
import { AgentProfile, ApiError, Credentials } from "../../api/client";
import AccessForm from "./AccessForm";
import RoleForm from "./RoleForm";
import { AgentDetails, loadAgent, loadDirectory, Role } from "./api";
import "./agents.css";

type Directory = Awaited<ReturnType<typeof loadDirectory>>;

export default function Agents({ credentials, profile, mode, onUnauthorized }: {
  credentials: Credentials; profile: AgentProfile; mode: "agents" | "permissions";
  onUnauthorized: () => void;
}) {
  const [directory, setDirectory] = useState<Directory | null>(null);
  const [selected, setSelected] = useState("");
  const [loadedDetail, setDetail] = useState<AgentDetails | null>(null);
  const [roleId, setRoleId] = useState("");
  const [query, setQuery] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<"create" | "access" | null>(null);
  const [creatingRole, setCreatingRole] = useState(false);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [version, setVersion] = useState(0);
  const detailsDialog = useRef<HTMLDialogElement>(null);
  const detail = loadedDetail?.id === selected ? loadedDetail : null;
  const unauthorized = useRef(onUnauthorized);
  unauthorized.current = onUnauthorized;
  const canRead = profile.permissions.includes("agents.read");
  const identity = directory?.identity ?? profile;
  const canManage = canRead && identity.permissions.includes("agents.admin");

  useEffect(() => {
    const controller = new AbortController();
    setDirectory(null); setDetail(null); setEditing(null); setError(null);
    if (!canRead) { setLoading(false); return () => controller.abort(); }
    setLoading(true);
    loadDirectory(credentials, controller.signal).then((next) => {
      if (!controller.signal.aborted) setDirectory(next);
    }).catch((cause: unknown) => {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && cause.status === 401) unauthorized.current();
      else setError(message(cause));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [credentials, canRead, version]);

  useEffect(() => {
    setDetail(null); setDetailError(null); setEditing(null);
    if (!selected || !directory) return;
    const controller = new AbortController();
    loadAgent(credentials, selected, controller.signal).then((next) => {
      if (!controller.signal.aborted) setDetail(next);
    }).catch((cause: unknown) => {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && cause.status === 401) unauthorized.current();
      else {
        setDetailError(message(cause));
        if (cause instanceof ApiError && cause.status === 403) {
          setDirectory(null);
          setError(message(cause));
        }
      }
    });
    return () => controller.abort();
  }, [credentials, selected, directory]);

  useEffect(() => {
    const element = detailsDialog.current;
    if (!element) return;
    if (detailsOpen && !element.open) {
      if (typeof element.showModal === "function") element.showModal();
      else element.setAttribute("open", "");
    } else if (!detailsOpen && element.open && typeof element.close === "function") {
      element.close();
    }
  }, [detailsOpen]);

  function closeDetails() {
    setDetailsOpen(false);
    setSelected("");
  }

  function saved(agent: AgentDetails) {
    setEditing(null);
    setNotice(`Saved ${agent.name}. Credentials remain operator-managed.`);
    setSelected(agent.id);
    setVersion((current) => current + 1);
  }

  function denied(status: number) {
    setEditing(null); setDetailsOpen(false); setSelected(""); setDetail(null); setDirectory(null);
    if (status === 401) unauthorized.current();
    else setError("Permission required. Your management access may have changed. Refresh to resolve current access.");
  }

  function roleSaved(role: Role) {
    setCreatingRole(false);
    setRoleId(role.id);
    setNotice(`Created role ${role.id}.`);
    setVersion((current) => current + 1);
  }

  const agents = directory?.agents.filter((agent) =>
    `${agent.id} ${agent.name} ${agent.teamId ?? ""}`.toLowerCase().includes(query.toLowerCase())) ?? [];
  const selectedRole = directory?.roles.find((role) => role.id === roleId);
  return <div className="identity-feature">
    <section className="page-heading">
      <div><div className="eyebrow heading-eyebrow">DETERMINISTIC ACCESS</div>
        <h1>{mode === "agents" ? "Agents" : "Roles & permissions"}</h1>
        <p>{mode === "agents" ? "Inspect identities, role grants and effective access. The backend enforces every operation." : "Inspect role grants and effective access."}</p></div>
      <div className="identity-actions">
        <button className="secondary-button" disabled={loading || !canRead} onClick={() => { setNotice(null); setVersion((current) => current + 1); }}>Refresh directory</button>
        {mode === "agents" && canManage && directory && <button className="primary-button" onClick={() => { setEditing("create"); setNotice(null); }}>Register agent</button>}
        {mode === "permissions" && canManage && directory && <button className="primary-button" onClick={() => { setCreatingRole(true); setNotice(null); }}>Create a new role</button>}
      </div>
    </section>
    {!canRead && <p role="alert">Permission required: agents.read. Your own effective permissions remain available below.</p>}
    {loading && <p role="status">Loading agents and grants...</p>}
    {error && <p className="inline-error" role="alert">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    {directory && <>
      {mode === "agents" && <section className="panel identity-panel" aria-label="Agent directory">
        {directory.agents.length === 1000 && <p role="status">The directory limit was reached; this may not be the complete inventory.</p>}
        <div className="identity-directory-search">
          <label>Search agents<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} /></label>
        </div>
        {agents.length === 0 && <p>No agents match this view.</p>}
        {agents.length > 0 && <div className="identity-table-scroll" role="region" aria-label="Agent list" tabIndex={0}>
          <table><caption>{agents.length} agents in this view</caption>
            <thead><tr><th scope="col">Agent</th><th scope="col">Team</th><th scope="col">Status</th><th scope="col">Roles</th><th scope="col">Details</th></tr></thead>
            <tbody>{agents.map((agent) => <tr key={agent.id}><th scope="row">{agent.name}<br /><small>{agent.id}</small></th>
              <td>{agent.teamId ?? "None"}</td><td>{agent.enabled ? "Enabled" : "Disabled"}</td><td>{agent.roles.join(", ") || "None"}</td>
              <td><button className="secondary-button" aria-label={`Inspect ${agent.id}`} onClick={() => { setSelected(agent.id); setDetailsOpen(true); setNotice(null); }}>Inspect</button></td>
            </tr>)}</tbody>
          </table>
        </div>}
      </section>}
      {mode === "agents" && selected && <dialog ref={detailsDialog} className="identity-dialog identity-panel identity-details-dialog" aria-label="Agent details" aria-busy={!detail && !detailError}
        onCancel={(event) => { event.preventDefault(); closeDetails(); }}
        onMouseDown={(event) => { if (event.target === event.currentTarget) closeDetails(); }}>
        <header className="identity-dialog-header">
          <h2>{detail?.name ?? "Agent details"}</h2>
          <button type="button" className="identity-dialog-close" aria-label="Close agent details" onClick={closeDetails}><X size={16} /></button>
        </header>
        {!detail && !detailError && <p role="status">Loading agent details...</p>}
        {detailError && <p role="alert">{detailError}</p>}
        {detail && <>
          <p>{detail.description}</p>
          <dl><dt>Agent ID</dt><dd>{detail.id}</dd><dt>Team</dt><dd>{detail.teamId ?? "None"}</dd>
            <dt>Status</dt><dd>{detail.enabled ? "Enabled" : "Disabled - cannot authenticate"}</dd>
            <dt>Roles</dt><dd>{detail.roles.join(", ") || "None"}</dd>
            <dt>Updated</dt><dd>{detail.updatedAt}</dd></dl>
          <div className="identity-actions">
            {canManage && detail.id !== profile.id && <button className="secondary-button" onClick={() => { setEditing("access"); setNotice(null); }}>Edit access</button>}
          </div>
          {detail.id === profile.id && <p>Your own access cannot be changed through this endpoint.</p>}
          {!canManage && <p>Read-only access. agents.admin is required for management.</p>}
          <div className="identity-permission-list" role="region" aria-label="Granted permissions">
            <h3>Granted permissions</h3>
            <div className="identity-permission-chips">
              {directory.permissions.filter((permission) => detail.permissions.includes(permission.id) && directory.roles.some((role) => detail.roles.includes(role.id) && role.permissions.includes(permission.id))).map((permission) => {
                const inherited = directory.roles.filter((role) => detail.roles.includes(role.id) && role.permissions.includes(permission.id)).map((role) => role.id);
                const source = inherited.length > 0 ? inherited.join(", ") : "Direct";
                return <span className="identity-permission-chip" key={permission.id}>
                  <span className="identity-permission-dot" aria-hidden="true" />
                  <span>{permission.id}</span><span className="identity-permission-separator">:</span><span className="identity-permission-source">{source}</span>
                </span>;
              })}
              {!directory.permissions.some((permission) => detail.permissions.includes(permission.id) && directory.roles.some((role) => detail.roles.includes(role.id) && role.permissions.includes(permission.id))) && <span className="identity-permission-empty">No role-inherited permissions</span>}
            </div>
          </div>
          {detail.directPermissions.length > 0 && <div className="identity-permission-list" role="region" aria-label="Direct grants">
            <h3>Direct grants</h3>
            <div className="identity-permission-chips">
              {detail.directPermissions.map((permission) => <span className="identity-permission-chip" key={permission}>
                <span className="identity-permission-dot" aria-hidden="true" />
                <span>{permission}</span><span className="identity-permission-separator">:</span><span className="identity-permission-source">Direct</span>
              </span>)}
            </div>
          </div>}
        </>}
      </dialog>}
      {mode === "agents" && editing && (editing === "create" || detail) && <AccessForm key={editing === "create" ? "create" : detail?.id} credentials={credentials}
        agent={editing === "access" ? detail : null} roles={directory.roles} permissions={directory.permissions}
        onSaved={saved} onCancel={() => setEditing(null)} onDenied={denied} />}
      {mode === "permissions" && <section className="panel identity-panel" aria-label="Roles and permission catalog">
        <h2>Role inspection</h2><p>Create roles using known permissions. Existing roles and permission catalog entries cannot be edited here.</p>
        <div className="role-inspection-row">
          <label className="role-inspection-select">Inspect role<select aria-label="Inspect role" value={roleId} onChange={(event) => setRoleId(event.target.value)}>
            <option value="">Select a role...</option>
            {directory.roles.map((role) => <option key={role.id} value={role.id}>{role.id}</option>)}
          </select></label>
          {selectedRole && <p className="role-inspection-description">{selectedRole.description}</p>}
        </div>
        {directory.roles.length === 0 && <p>No roles configured.</p>}
        <div className="role-permission-table" role="region" aria-label="Permissions for selected role" tabIndex={0}>
          <table><thead><tr><th scope="col">Permission</th><th scope="col">Description</th></tr></thead>
            <tbody>{selectedRole?.permissions.map((permissionId) => {
              const permission = directory.permissions.find((item) => item.id === permissionId);
              return <tr key={permissionId}><th scope="row">{permissionId}</th><td>{permission?.description ?? "Description unavailable."}</td></tr>;
            })}</tbody>
          </table>
          {!selectedRole && <p className="identity-multiselect-empty">Select a role to view permissions</p>}
          {selectedRole && selectedRole.permissions.length === 0 && <p className="identity-multiselect-empty">No permissions assigned</p>}
        </div>
        {directory.permissions.length === 0 && <p>No permissions configured.</p>}
      </section>}
      {mode === "permissions" && creatingRole && <RoleForm credentials={credentials} permissions={directory.permissions}
        onSaved={roleSaved} onCancel={() => setCreatingRole(false)} onDenied={denied} />}
    </>}
  </div>;
}

function message(cause: unknown) {
  if (cause instanceof ApiError) {
    if (cause.status === 403) return "Permission required: agents.read. Refresh after your access has been restored.";
    if (cause.status === 404) return "This agent no longer exists or is inaccessible.";
    if (cause.status >= 500) return "The Brain identity service is unavailable. Retry using Refresh directory.";
  }
  return "Could not load identities. Check your connection and retry using Refresh directory.";
}
