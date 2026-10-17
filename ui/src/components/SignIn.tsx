import { BrainCircuit, LockKeyhole, ShieldCheck } from "lucide-react";
import { FormEvent, useState } from "react";
import { Credentials } from "../api/client";
import { Theme } from "../hooks/useTheme";
import ThemeToggle from "./ThemeToggle";

interface SignInProps {
  error: string | null;
  onSubmit: (credentials: Credentials) => Promise<void>;
  theme: Theme;
  onToggleTheme: () => void;
}

export default function SignIn({ error, onSubmit, theme, onToggleTheme }: SignInProps) {
  const [token, setToken] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    try {
      await onSubmit({ token });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="signin-page">
      <section className="signin-card" aria-labelledby="signin-heading">
        <div className="signin-brand">
          <span className="brand-mark"><BrainCircuit size={24} /></span>
          <span>HOMELAB <b>BRAIN</b></span>
          <ThemeToggle theme={theme} onToggle={onToggleTheme} />
        </div>
        <div className="signin-intro">
          <span className="eyebrow">YOUR KNOWLEDGE CONTROL PLANE</span>
          <h1 id="signin-heading">A clearer view of your Brain.</h1>
          <p>Enter your administrator token to access the Brain control plane.</p>
        </div>
        <form className="signin-form" onSubmit={submit}>
          <label htmlFor="admin-token">Admin token</label>
          <div className="token-field">
            <LockKeyhole size={17} aria-hidden="true" />
            <input
              id="admin-token"
              autoComplete="off"
              minLength={32}
              maxLength={1024}
              onChange={(event) => setToken(event.target.value)}
              placeholder="Enter your admin token"
              required
              type="password"
              value={token}
            />
          </div>
          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="primary-button signin-button" disabled={submitting} type="submit">
            {submitting ? "Signing in..." : "Sign in to Brain"}
          </button>
        </form>
        <div className="signin-security">
          <ShieldCheck size={16} />
          <span>Credentials stay in this tab and are never saved to this browser.</span>
        </div>
      </section>
      <aside className="signin-aside" aria-label="Platform information">
        <div className="aside-orbit orbit-one" />
        <div className="aside-orbit orbit-two" />
        <div className="aside-content">
          <span className="eyebrow">ONE SHARED SOURCE OF TRUTH</span>
          <h2>Knowledge in context.<br />Operations in view.</h2>
          <p>Bring agents, skills, tools, and the systems they depend on into one calm, connected workspace.</p>
          <div className="aside-status">
            <span className="status-dot" />
            <span>Deterministic access. Your permissions apply everywhere.</span>
          </div>
        </div>
        <span className="aside-footer">BUILT FOR YOUR HOMELAB</span>
      </aside>
    </main>
  );
}
