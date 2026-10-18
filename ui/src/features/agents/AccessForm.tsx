import { FormEvent, useEffect, useRef, useState } from "react";
import { ApiError, Credentials } from "../../api/client";
import { AccessInput, AgentDetails, CreateInput, Permission, Role, saveAgent } from "./api";

export default function AccessForm({ credentials, agent, roles, permissions, onSaved, onCancel, onDenied }: {
  credentials: Credentials; agent: AgentDetails | null; roles: Role[]; permissions: Permission[];
  onSaved: (agent: AgentDetails) => void; onCancel: () => void; onDenied: (status: number) => void;
}) {
  const [id, setId] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [team, setTeam] = useState("");
  const [selectedRoles, setSelectedRoles] = useState(agent?.roles ?? []);
  const [direct, setDirect] = useState(agent?.directPermissions ?? []);
  const [enabled, setEnabled] = useState(agent?.enabled ?? true);
  const [review, setReview] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const pending = useRef<AbortController | null>(null);
  const effective = [...new Set([...direct, ...roles.filter((role) => selectedRoles.includes(role.id)).flatMap((role) => role.permissions)])].sort();
  useEffect(() => () => pending.current?.abort(), []);

  function toggle(value: string, current: string[], set: (values: string[]) => void) {
    set(current.includes(value) ? current.filter((item) => item !== value) : [...current, value]);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    if (!review) { setReview(true); return; }
    const controller = new AbortController();
    pending.current = controller;
    setSaving(true);
    setError(null);
    const input: AccessInput | CreateInput = agent
      ? { roles: selectedRoles, permissions: direct, enabled }
      : { id, name, description, teamId: team || null, roles: selectedRoles, permissions: direct };
    try {
      const saved = await saveAgent(credentials, input, agent?.id ?? null, controller.signal);
      if (!controller.signal.aborted) onSaved(saved);
    } catch (cause) {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && (cause.status === 401 || cause.status === 403)) {
        onDenied(cause.status);
        return;
      }
      setError(cause instanceof ApiError && cause.status === 409
        ? "An agent with this ID already exists. Choose another ID."
        : cause instanceof ApiError && cause.status === 400
          ? "The identity or grants are invalid. Refresh the directory and check the selected values."
          : cause instanceof ApiError && cause.status === 404
            ? "This agent no longer exists. Cancel and refresh the directory."
            : "Could not save the agent. The outcome may be unknown; refresh the directory before retrying.");
    } finally {
      if (!controller.signal.aborted) setSaving(false);
    }
  }

  return <section className="panel identity-panel" aria-label={agent ? "Edit access" : "Register agent"}>
    <h2>{agent ? `Edit access for ${agent.name}` : "Register agent"}</h2>
    <p>Credentials are provisioned separately by an operator. Brain does not issue or display tokens here.</p>
    <form onSubmit={submit} className="identity-form">
      <fieldset disabled={saving}>
        <legend className="sr-only">Agent settings</legend>
        {!review ? <>
          {!agent && <div className="identity-fields">
            <label>Agent ID<input required pattern="[a-z0-9][a-z0-9-]{0,99}" maxLength={100} value={id} onChange={(event) => setId(event.target.value)} /></label>
            <label>Name<input required maxLength={200} value={name} onChange={(event) => setName(event.target.value)} /></label>
            <label>Team ID (optional)<input pattern="[a-z0-9][a-z0-9-]{0,99}" maxLength={100} value={team} onChange={(event) => setTeam(event.target.value)} /></label>
            <label>Description (optional)<textarea maxLength={2000} value={description} onChange={(event) => setDescription(event.target.value)} /></label>
          </div>}
          {agent && <label className="identity-check"><input type="checkbox" checked={enabled} onChange={(event) => setEnabled(event.target.checked)} />Agent enabled</label>}
          <div className="identity-grants">
            <fieldset><legend>Roles</legend>
              {roles.length === 0 && <p>No roles configured.</p>}
              {roles.map((role) => <label className="identity-check" key={role.id}>
                <input type="checkbox" checked={selectedRoles.includes(role.id)} onChange={() => toggle(role.id, selectedRoles, setSelectedRoles)} />{role.id}
              </label>)}
            </fieldset>
            <fieldset><legend>Direct permissions</legend>
              {permissions.length === 0 && <p>No permissions configured.</p>}
              {permissions.map((permission) => <label className="identity-check" key={permission.id}>
                <input type="checkbox" checked={direct.includes(permission.id)} onChange={() => toggle(permission.id, direct, setDirect)} />{permission.id}
              </label>)}
            </fieldset>
          </div>
        </> : <div className="identity-review" role="region" aria-label="Review access changes">
          <h3>{agent ? "Replace all access" : `Create ${id}`}</h3>
          <p>{agent ? "This replaces all roles, direct permissions and enabled state, not just changed fields. Concurrent operator updates are not merged. Confirm only after coordinating access changes." : `${name} will be registered as an enabled identity without a credential.`}</p>
          <dl><dt>Roles</dt><dd>{selectedRoles.join(", ") || "None"}</dd>
            <dt>Direct permissions</dt><dd>{direct.join(", ") || "None"}</dd>
            <dt>Enabled</dt><dd>{enabled ? "Yes" : "No - authentication will be denied"}</dd>
            <dt>Effective access preview</dt><dd>{effective.join(", ") || "None"}</dd></dl>
          <p>The server validates current roles, grants and your authorization when saving.</p>
        </div>}
        {error && <p role="alert" className="inline-error">{error}</p>}
        <div className="identity-actions">
          <button className="primary-button" type="submit" disabled={saving || selectedRoles.length > 100 || direct.length > 100}>
            {saving ? "Saving..." : review ? agent ? "Confirm replacement" : "Confirm registration" : "Review changes"}
          </button>
          {review && <button className="secondary-button" type="button" onClick={() => { setReview(false); setError(null); }}>Back to editing</button>}
          <button className="secondary-button" type="button" onClick={onCancel}>Cancel</button>
        </div>
        {(selectedRoles.length > 100 || direct.length > 100) && <p role="alert">Select at most 100 roles and 100 direct permissions.</p>}
      </fieldset>
    </form>
  </section>;
}
