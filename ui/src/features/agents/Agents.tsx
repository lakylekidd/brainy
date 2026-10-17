import { useEffect, useRef, useState } from "react";
import { AgentProfile, ApiError, Credentials } from "../../api/client";
import AccessForm from "./AccessForm";
import { AgentDetails, loadAgent, loadDirectory } from "./api";
import "./agents.css";

type Directory = Awaited<ReturnType<typeof loadDirectory>>;

export default function Agents({ credentials, profile, mode, onUnauthorized, onFocus }: {
  credentials: Credentials; profile: AgentProfile; mode: "agents" | "permissions";
  onUnauthorized: () => void; onFocus: (id: string) => void;
}) {
  const [directory, setDirectory] = useState<Directory | null>(null);
  const [selected, setSelected] = useState("");
  const [loadedDetail, setDetail] = useState<AgentDetails | null>(null);
  const [roleId, setRoleId] = useState("");
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState("all");
  const [error, setError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<"create" | "access" | null>(null);
  const [version, setVersion] = useState(0);
  const [detailVersion, setDetailVersion] = useState(0);
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
  }, [credentials, selected, directory, detailVersion]);

  function saved(agent: AgentDetails) {
    setEditing(null);
    setNotice(`Saved ${agent.name}. Credentials remain operator-managed.`);
    setSelected(agent.id);
    setVersion((current) => current + 1);
  }

  function denied(status: number) {
    setEditing(null); setDetail(null); setDirectory(null);
    if (status === 401) unauthorized.current();
    else setError("Permission required. Your management access may have changed. Refresh to resolve current access.");
  }

  const agents = directory?.agents.filter((agent) =>
    `${agent.id} ${agent.name} ${agent.teamId ?? ""}`.toLowerCase().includes(query.toLowerCase()) &&
    (filter === "all" || (filter === "enabled" ? agent.enabled : !agent.enabled))) ?? [];
  const selectedRole = directory?.roles.find((role) => role.id === roleId);
  return <div className="identity-feature">
    <section className="page-heading">
      <div><div className="eyebrow heading-eyebrow">DETERMINISTIC ACCESS</div>
        <h1>{mode === "agents" ? "Agents" : "Roles & permissions"}</h1>
        <p>Inspect identities, role grants and effective access. The backend enforces every operation.</p></div>
      <div className="identity-actions">
        <button className="secondary-button" disabled={loading || !canRead} onClick={() => { setNotice(null); setVersion((current) => current + 1); }}>Refresh directory</button>
        {canManage && directory && <button className="primary-button" onClick={() => { setEditing("create"); setNotice(null); }}>Register agent</button>}
      </div>
    </section>
    {!canRead && <p role="alert">Permission required: agents.read. Your own effective permissions remain available below.</p>}
    {loading && <p role="status">Loading agents and grants...</p>}
    {error && <p className="inline-error" role="alert">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    <section className="panel identity-panel" aria-label="Current identity">
      <h2>Current identity</h2><p>{identity.id} - permissions resolved at {directory ? "the latest directory refresh" : "sign-in"}.</p>
      <p>{identity.permissions.join(", ") || "No grants"}</p>
      {!directory && <p>Reload the page to resolve newly granted access; a failed refresh does not verify this sign-in summary.</p>}
      <p>Grants are an exact union, not wildcards or a role-name bypass. Enabled state, resource visibility and operation rules still apply.</p>
    </section>
    {directory && <>
      <section className="panel identity-panel" aria-label="Agent directory">
        <h2>Agent directory</h2>
        <p>Up to 1,000 identities, including disabled agents. Search and filters apply to this returned list.</p>
        {directory.agents.length === 1000 && <p role="status">The directory limit was reached; this may not be the complete inventory. Independent detail lookup remains available.</p>}
        <div className="identity-fields">
          <label>Search agents<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} /></label>
          <label>Agent status<select aria-label="Agent status" value={filter} onChange={(event) => setFilter(event.target.value)}>
            <option value="all">All statuses</option><option value="enabled">Enabled</option><option value="disabled">Disabled</option>
          </select></label>
          <label>Inspect agent<select aria-label="Inspect agent" value={agents.some((agent) => agent.id === selected) ? selected : ""} onChange={(event) => { setSelected(event.target.value); setNotice(null); }}>
            <option value="">Select an agent...</option>
            {agents.map((agent) => <option value={agent.id} key={agent.id}>{agent.name} ({agent.id}) - {agent.enabled ? "Enabled" : "Disabled"}</option>)}
          </select></label>
        </div>
        {agents.length === 0 && <p>No agents match this view.</p>}
        {agents.length > 0 && <div className="identity-table-scroll" role="region" aria-label="Agent list" tabIndex={0}>
          <table><caption>{agents.length} agents in this view</caption>
            <thead><tr><th scope="col">Agent</th><th scope="col">Team</th><th scope="col">Status</th><th scope="col">Roles</th><th scope="col">Details</th></tr></thead>
            <tbody>{agents.map((agent) => <tr key={agent.id}><th scope="row">{agent.name}<br /><small>{agent.id}</small></th>
              <td>{agent.teamId ?? "None"}</td><td>{agent.enabled ? "Enabled" : "Disabled"}</td><td>{agent.roles.join(", ") || "None"}</td>
              <td><button className="secondary-button" aria-label={`Inspect ${agent.id}`} onClick={() => { setSelected(agent.id); setNotice(null); }}>Inspect</button></td>
            </tr>)}</tbody>
          </table>
        </div>}
        <form className="identity-lookup" onSubmit={(event) => {
          event.preventDefault();
          const id = new FormData(event.currentTarget).get("id");
          if (typeof id === "string") { setSelected(id.trim()); setDetailVersion((current) => current + 1); }
        }}>
          <label>Look up agent ID<input name="id" required maxLength={100} pattern="[a-z0-9][a-z0-9-]{0,99}" /></label>
          <button className="secondary-button">Inspect by ID</button>
        </form>
      </section>
      {selected && <section className="panel identity-panel" aria-label="Agent details" aria-busy={!detail && !detailError}>
        {!detail && !detailError && <p role="status">Loading agent details...</p>}
        {detailError && <p role="alert">{detailError}</p>}
        {detail && <>
          <h2>{detail.name}</h2><p>{detail.description}</p>
          <dl><dt>Agent ID</dt><dd>{detail.id}</dd><dt>Team</dt><dd>{detail.teamId ?? "None"}</dd>
            <dt>Status</dt><dd>{detail.enabled ? "Enabled" : "Disabled - cannot authenticate"}</dd>
            <dt>Roles</dt><dd>{detail.roles.join(", ") || "None"}</dd>
            <dt>Direct grants</dt><dd>{detail.directPermissions.join(", ") || "None"}</dd>
            <dt>Effective permissions</dt><dd>{detail.permissions.join(", ") || "None"}</dd>
            <dt>Updated</dt><dd>{detail.updatedAt}</dd></dl>
          <div className="identity-actions">
            <button className="secondary-button" onClick={() => onFocus(`agent:${detail.id}`)}>Focus agent graph</button>
            {canManage && detail.id !== profile.id && <button className="secondary-button" onClick={() => { setEditing("access"); setNotice(null); }}>Edit access</button>}
          </div>
          {detail.id === profile.id && <p>Your own access cannot be changed through this endpoint.</p>}
          {!canManage && <p>Read-only access. agents.admin is required for management.</p>}
          <div className="identity-table-scroll" role="region" aria-label="Agent permission matrix" tabIndex={0}>
            <table><caption>Agent permission matrix</caption>
              <thead><tr><th scope="col">Permission</th><th scope="col">Inherited from roles</th><th scope="col">Direct</th><th scope="col">Effective</th></tr></thead>
              <tbody>{directory.permissions.map((permission) => <tr key={permission.id}>
                <th scope="row">{permission.id}</th>
                <td>{directory.roles.filter((role) => detail.roles.includes(role.id) && role.permissions.includes(permission.id)).map((role) => role.id).join(", ") || "None"}</td>
                <td>{detail.directPermissions.includes(permission.id) ? "Granted" : "Not granted"}</td>
                <td>{detail.permissions.includes(permission.id) ? "Granted" : "Not granted"}</td>
              </tr>)}</tbody>
            </table>
          </div>
        </>}
      </section>}
      {editing && (editing === "create" || detail) && <AccessForm key={editing === "create" ? "create" : detail?.id} credentials={credentials}
        agent={editing === "access" ? detail : null} roles={directory.roles} permissions={directory.permissions}
        onSaved={saved} onCancel={() => setEditing(null)} onDenied={denied} />}
      {mode === "permissions" && <section className="panel identity-panel" aria-label="Roles and permission catalog">
        <h2>Role inspection</h2><p>Role definitions and permission catalog entries are operator-managed; no edit operation is exposed.</p>
        <label>Inspect role<select aria-label="Inspect role" value={roleId} onChange={(event) => setRoleId(event.target.value)}>
          <option value="">Select a role...</option>
          {directory.roles.map((role) => <option key={role.id} value={role.id}>{role.id}</option>)}
        </select></label>
        {directory.roles.length === 0 && <p>No roles configured.</p>}
        {selectedRole && <div><h3>{selectedRole.id}</h3><p>{selectedRole.description}</p>
          <p>{selectedRole.permissions.join(", ") || "No grants"}</p></div>}
        <div className="identity-table-scroll" role="region" aria-label="Role permission matrix" tabIndex={0}>
          <table><caption>Role permission matrix</caption><thead><tr><th scope="col">Permission</th><th scope="col">Description</th>
            {directory.roles.map((role) => <th scope="col" key={role.id}>{role.id}</th>)}</tr></thead>
            <tbody>{directory.permissions.map((permission) => <tr key={permission.id}><th scope="row">{permission.id}</th><td>{permission.description}</td>
              {directory.roles.map((role) => <td key={role.id}>{role.permissions.includes(permission.id) ? "Granted" : "Not granted"}</td>)}</tr>)}</tbody>
          </table>
        </div>
        {directory.permissions.length === 0 && <p>No permissions configured.</p>}
      </section>}
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
