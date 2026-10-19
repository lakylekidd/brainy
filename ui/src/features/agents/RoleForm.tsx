import { FormEvent, useEffect, useRef, useState } from "react";
import { X } from "lucide-react";
import { ApiError, Credentials } from "../../api/client";
import { createRole, Permission, Role } from "./api";
import SearchMultiSelect from "./SearchMultiSelect";

export default function RoleForm({ credentials, permissions, onSaved, onCancel, onDenied }: {
  credentials: Credentials; permissions: Permission[]; onSaved: (role: Role) => void;
  onCancel: () => void; onDenied: (status: number) => void;
}) {
  const [id, setId] = useState("");
  const [description, setDescription] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const [review, setReview] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  const pending = useRef<AbortController | null>(null);
  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (typeof element.showModal === "function") element.showModal();
    else element.setAttribute("open", "");
    return () => { if (element.open && typeof element.close === "function") element.close(); };
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    if (!review) { setReview(true); return; }
    const controller = new AbortController();
    pending.current = controller;
    setSaving(true); setError(null);
    try {
      const role = await createRole(credentials, { id, description, permissions: selected }, controller.signal);
      if (!controller.signal.aborted) onSaved(role);
    } catch (cause) {
      if (controller.signal.aborted) return;
      if (cause instanceof ApiError && (cause.status === 401 || cause.status === 403)) { onDenied(cause.status); return; }
      setError(cause instanceof ApiError && cause.status === 409
        ? "A role with this ID already exists. Choose another ID."
        : cause instanceof ApiError && cause.status === 400
          ? "The role or permission selection is invalid. Refresh the directory and check the selected values."
          : "Could not create the role. Refresh the directory before retrying.");
      setReview(false);
    } finally {
      if (!controller.signal.aborted) setSaving(false);
    }
  }

  return <dialog ref={dialog} className="identity-dialog identity-panel" aria-label="Create a new role"
    onCancel={(event) => { event.preventDefault(); onCancel(); }}
    onMouseDown={(event) => { if (event.target === event.currentTarget) onCancel(); }}>
    <header className="identity-dialog-header">
      <h2>Create a new role</h2>
      <button type="button" className="identity-dialog-close" aria-label="Close role creation" onClick={onCancel}><X size={16} /></button>
    </header>
    <p>Choose known permissions for this role. Creating a role does not assign it to any agents.</p>
    <form onSubmit={submit} className="identity-form">
      <fieldset disabled={saving}>
        <legend className="sr-only">New role settings</legend>
        {!review ? <div className="identity-fields">
          <label>Role ID<input required pattern="[a-z0-9][a-z0-9-]{0,99}" maxLength={100} value={id} onChange={(event) => setId(event.target.value)} /></label>
          <label>Description<input required maxLength={2000} value={description} onChange={(event) => setDescription(event.target.value)} /></label>
          <label>Permissions
            {permissions.length > 0
              ? <SearchMultiSelect label="Search permissions" options={permissions.map((permission) => permission.id)} selected={selected} onChange={setSelected} />
              : <span>No permissions configured.</span>}
          </label>
        </div> : <div className="identity-review" role="region" aria-label="Review new role">
          <h3>{id}</h3><p>{description}</p>
          <dl><dt>Permissions</dt><dd>{selected.join(", ") || "None"}</dd></dl>
          <p>The server will revalidate your authorization and every permission when saving.</p>
        </div>}
        {error && <p role="alert" className="inline-error">{error}</p>}
        <div className="identity-actions">
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Creating..." : review ? "Confirm role creation" : "Review role"}</button>
          {review && <button className="secondary-button" type="button" onClick={() => setReview(false)}>Back to editing</button>}
          <button className="secondary-button" type="button" onClick={onCancel}>Cancel</button>
        </div>
      </fieldset>
    </form>
  </dialog>;
}
