import { useId, useState } from "react";
import { X } from "lucide-react";

export default function SearchMultiSelect({ label, options, selected, onChange }: {
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
