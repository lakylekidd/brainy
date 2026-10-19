import { FormEvent, useEffect, useId, useRef, useState } from "react";
import { X } from "lucide-react";
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
  const dialog = useRef<HTMLDialogElement>(null);
  const effective = [...new Set([...direct, ...roles.filter((role) => selectedRoles.includes(role.id)).flatMap((role) => role.permissions)])].sort();
  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    if (agent) return;
    const element = dialog.current;
    if (!element) return;
    if (typeof element.showModal === "function") element.showModal();
    else element.setAttribute("open", "");
    return () => { if (element.open && typeof element.close === "function") element.close(); };
  }, [agent]);

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

  const content = <>
    {agent ? <h2>Edit access for {agent.name}</h2> : <header className="identity-dialog-header">
      <h2>Register agent</h2>
      <button type="button" className="identity-dialog-close" aria-label="Close registration" onClick={onCancel}><X size={16} /></button>
    </header>}
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
              {roles.length > 0 && <SearchMultiSelect label="Search roles" options={roles.map((role) => role.id)}
                selected={selectedRoles} onChange={setSelectedRoles} />}
            </fieldset>
            <fieldset><legend>Direct permissions</legend>
              {permissions.length === 0 && <p>No permissions configured.</p>}
              {permissions.length > 0 && <SearchMultiSelect label="Search direct permissions" options={permissions.map((permission) => permission.id)}
                selected={direct} onChange={setDirect} />}
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
  </>;

  if (!agent) return <dialog ref={dialog} className="identity-dialog identity-panel" aria-label="Register agent"
    onCancel={(event) => { event.preventDefault(); onCancel(); }}
    onMouseDown={(event) => { if (event.target === event.currentTarget) onCancel(); }}>
    {content}
  </dialog>;

  return <section className="panel identity-panel" aria-label="Edit access">{content}</section>;
}

function SearchMultiSelect({ label, options, selected, onChange }: {
  label: string; options: string[]; selected: string[]; onChange: (values: string[]) => void;
}) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const id = useId();
  const available = options.filter((option) => !selected.includes(option) && option.toLowerCase().includes(query.trim().toLowerCase()));

  function add(value: string) {
    if (!selected.includes(value) && selected.length < 100) onChange([...selected, value]);
    setQuery("");
    setOpen(false);
  }

  return <div className="identity-multiselect">
    <div className="identity-multiselect-input">
      {selected.map((value) => <span className="identity-chip" key={value}>{value}
        <button type="button" aria-label={`Remove ${value}`} onClick={() => onChange(selected.filter((item) => item !== value))}><X size={12} /></button>
      </span>)}
      <input id={id} type="search" aria-label={label} aria-autocomplete="list" aria-expanded={open && query.trim().length > 0}
        aria-controls={`${id}-options`} autoComplete="off" placeholder={selected.length ? "Add another..." : "Type to search..."}
        value={query} onFocus={() => setOpen(true)} onChange={(event) => { setQuery(event.target.value); setOpen(true); }}
        onKeyDown={(event) => {
          if (event.key === "Escape") setOpen(false);
          if (event.key === "Enter" && open && available.length > 0) { event.preventDefault(); add(available[0]); }
        }} />
    </div>
    {open && query.trim() && <div className="identity-multiselect-options" id={`${id}-options`} role="listbox" aria-label={`Available ${label.replace(/^Search /, "")}`}>
      {available.length > 0 ? available.map((option) => <button type="button" role="option" aria-selected="false" key={option}
        onMouseDown={(event) => event.preventDefault()} onClick={() => add(option)}>{option}</button>)
        : <span className="identity-multiselect-empty">No matching options</span>}
    </div>}
  </div>;
}
