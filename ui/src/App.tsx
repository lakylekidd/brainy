import { lazy, Suspense, useEffect, useState } from "react";
import { ApiError, authenticate, Credentials } from "./api/client";
import AppShell, { PageId } from "./components/AppShell";
import SignIn from "./components/SignIn";
import { useTheme } from "./hooks/useTheme";

const Agents = lazy(() => import("./features/agents/Agents"));
const PavDashboard = lazy(() => import("./features/pav/PavDashboard"));
const SESSION_TOKEN_KEY = "brain-ui-admin-token";

function pageFromLocation(): PageId {
  const path = window.location.pathname.replace(/^\/+|\/+$/g, "");
  const page = path === "dashboard" || path === "" ? "pav" : path as PageId;
  return ["pav", "agents", "skills", "tools", "connections", "permissions", "proposals", "metadata", "operations", "audit", "settings"].includes(page) ? page : "pav";
}

function pathForPage(page: PageId) {
  return page === "pav" ? "/dashboard" : `/${page}`;
}

function readSessionCredentials(): Credentials | null {
  try {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
    return token ? { token } : null;
  } catch {
    return null;
  }
}

function saveSessionCredentials(credentials: Credentials | null) {
  try {
    if (credentials) sessionStorage.setItem(SESSION_TOKEN_KEY, credentials.token);
    else sessionStorage.removeItem(SESSION_TOKEN_KEY);
  } catch {
    console.warn("The sign-in token could not be saved for this tab session.");
  }
}

export default function App() {
  const { theme, toggleTheme } = useTheme();
  const [initialCredentials] = useState(readSessionCredentials);
  const [credentials, setCredentials] = useState<Credentials | null>(initialCredentials);
  const [profile, setProfile] = useState<Awaited<ReturnType<typeof authenticate>> | null>(null);
  const [page, setPage] = useState<PageId>(pageFromLocation);
  const [signInError, setSignInError] = useState<string | null>(null);
  const [restoringSession, setRestoringSession] = useState(Boolean(initialCredentials));

  useEffect(() => {
    const onPopState = () => setPage(pageFromLocation());
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  function navigate(page: PageId) {
    window.history.pushState({}, "", pathForPage(page));
    setPage(page);
  }

  useEffect(() => {
    if (!initialCredentials) return;
    let cancelled = false;
    authenticate(initialCredentials)
      .then((identity) => {
        if (!cancelled) setProfile(identity);
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setCredentials(null);
          if (cause instanceof ApiError && cause.status === 401) {
            saveSessionCredentials(null);
            setSignInError("Your saved sign-in is no longer valid. Enter your admin token again.");
          } else {
            setSignInError("Could not validate your saved sign-in. Check the Brain API connection and try again.");
          }
        }
      })
      .finally(() => {
        if (!cancelled) setRestoringSession(false);
      });
    return () => { cancelled = true; };
  }, [initialCredentials]);

  async function signIn(nextCredentials: Credentials) {
    setSignInError(null);
    try {
      const identity = await authenticate(nextCredentials);
      saveSessionCredentials(nextCredentials);
      setCredentials(nextCredentials);
      setProfile(identity);
      navigate("pav");
    } catch {
      setSignInError("Could not sign in. Check your admin token and the Brain API connection.");
    }
  }

  function signOut(message?: string) {
    saveSessionCredentials(null);
    setCredentials(null);
    setProfile(null);
    navigate("pav");
    setSignInError(message ?? null);
  }

  if (restoringSession) {
    return <main className="signin-page"><section className="signin-card"><p role="status">Restoring your sign-in for this tab…</p></section></main>;
  }

  if (!credentials || !profile) {
    return <SignIn error={signInError} onSubmit={signIn} theme={theme} onToggleTheme={toggleTheme} />;
  }

  return (
    <AppShell
      onNavigate={navigate}
      onSignOut={() => signOut()}
      page={page}
      profile={profile}
      theme={theme}
      onToggleTheme={toggleTheme}
    >
      {page === "pav" ? (
        <Suspense fallback={<p role="status">Loading demo vault...</p>}>
          <PavDashboard theme={theme} />
        </Suspense>
      ) : page === "agents" || page === "permissions" ? (
        <Suspense fallback={<p role="status">Loading identity feature...</p>}>
          <Agents credentials={credentials} profile={profile} mode={page}
            onUnauthorized={() => signOut("Your credentials are no longer valid. Connect again.")} />
        </Suspense>
      ) : (
        <section className="placeholder-page">
          <span className="placeholder-icon">◌</span>
          <span className="eyebrow">BRAIN WORKSPACE</span>
          <h1>{pageTitle(page)}</h1>
          <p>This area is reserved for a later Brain UI milestone.</p>
          <button className="secondary-button" onClick={() => navigate("pav")}>Back to PAV OS</button>
        </section>
      )}
    </AppShell>
  );
}

function pageTitle(page: PageId): string {
  return {
    pav: "PAV OS",
    agents: "Agents",
    skills: "Skills",
    tools: "Tools",
    connections: "Connections",
    permissions: "Permissions",
    proposals: "Proposals",
    metadata: "Metadata",
    operations: "Operations",
    audit: "Audit",
    settings: "Settings",
  }[page];
}
