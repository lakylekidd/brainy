import { lazy, Suspense, useEffect, useState } from "react";
import { ApiError, authenticate, Credentials, DashboardData, loadDashboard } from "./api/client";
import AppShell, { PageId } from "./components/AppShell";
import SignIn from "./components/SignIn";
import Dashboard from "./features/dashboard/Dashboard";
import { useTheme } from "./hooks/useTheme";

const KnowledgeGraph = lazy(() => import("./features/graph/KnowledgeGraph"));

export default function App() {
  const { theme, toggleTheme } = useTheme();
  const [credentials, setCredentials] = useState<Credentials | null>(null);
  const [profile, setProfile] = useState<Awaited<ReturnType<typeof authenticate>> | null>(null);
  const [page, setPage] = useState<PageId>("dashboard");
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState<string | null>(null);
  const [signInError, setSignInError] = useState<string | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);

  async function signIn(nextCredentials: Credentials) {
    setSignInError(null);
    try {
      const identity = await authenticate(nextCredentials);
      setCredentials(nextCredentials);
      setProfile(identity);
      setPage("dashboard");
    } catch {
      setSignInError("Could not sign in. Check your admin token and the Brain API connection.");
    }
  }

  function signOut(message?: string) {
    setCredentials(null);
    setProfile(null);
    setData(null);
    setPage("dashboard");
    setDashboardError(null);
    setSignInError(message ?? null);
  }

  useEffect(() => {
    if (!credentials) return;
    const controller = new AbortController();
    setLoading(true);
    setDashboardError(null);
    loadDashboard(credentials, controller.signal)
      .then((dashboard) => {
        if (!controller.signal.aborted) setData(dashboard);
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && error.status === 401) {
          signOut("Your credentials are no longer valid. Connect again.");
          return;
        }
        setDashboardError("Some dashboard data could not be refreshed. Your accessible data remains available.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [credentials, refreshVersion]);

  if (!credentials || !profile) {
    return <SignIn error={signInError} onSubmit={signIn} theme={theme} onToggleTheme={toggleTheme} />;
  }

  return (
    <AppShell
      onNavigate={setPage}
      onSignOut={() => signOut()}
      page={page}
      profile={profile}
      theme={theme}
      onToggleTheme={toggleTheme}
    >
      {page === "dashboard" ? (
        <Dashboard
          data={data}
          error={dashboardError}
          loading={loading}
          onNavigate={setPage}
          onRefresh={() => setRefreshVersion((version) => version + 1)}
        />
      ) : page === "graph" ? (
        <Suspense fallback={<p role="status">Loading graph feature…</p>}>
          <KnowledgeGraph credentials={credentials} theme={theme} onUnauthorized={() => signOut("Your credentials are no longer valid. Connect again.")} />
        </Suspense>
      ) : (
        <section className="placeholder-page">
          <span className="placeholder-icon">◌</span>
          <span className="eyebrow">MILESTONE 1 · SHELL + DASHBOARD</span>
          <h1>{pageTitle(page)}</h1>
          <p>This area is reserved for a later Brain UI milestone.</p>
          <button className="secondary-button" onClick={() => setPage("dashboard")}>Back to dashboard</button>
        </section>
      )}
    </AppShell>
  );
}

function pageTitle(page: PageId): string {
  return {
    dashboard: "Dashboard",
    graph: "Knowledge graph",
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
