import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import Dashboard from "./Dashboard";
import { DashboardData } from "../../api/client";

const data: DashboardData = {
  api: { status: "ready", value: { status: "ready" } },
  agents: { status: "ready", value: [{ id: "codex", name: "Codex", enabled: true }] },
  skills: { status: "ready", value: [{ type: "skill", id: "incident-triage", title: "Incident triage", content: "Not shown" }] },
  tools: { status: "ready", value: [] },
  proposals: { status: "ready", value: [{
    id: "proposal-1",
    path: "Operations/incident.md",
    operation: "append",
    status: "pending",
    createdAt: new Date().toISOString(),
  }] },
  metadata: { status: "unavailable", reason: "Permission required" },
  audit: { status: "ready", value: [{
    action: "brain.knowledge.proposed",
    timestamp: new Date().toISOString(),
    result: "success",
    metadata: { token: "must-not-be-rendered", authorization: "Bearer secret" },
  }] },
  traces: { status: "ready", value: [] },
};

describe("Dashboard", () => {
  afterEach(cleanup);
  it("shows available metrics and marks inaccessible data without inventing counts", () => {
    render(
      <Dashboard
        data={data}
        error={null}
        loading={false}
        onNavigate={vi.fn()}
        onRefresh={vi.fn()}
      />,
    );

    expect(screen.getByText("Incident triage")).toBeInTheDocument();
    expect(screen.getByText("Operations/incident.md")).toBeInTheDocument();
    expect(screen.getByText("Permission required")).toBeInTheDocument();
    expect(screen.getByText("No document-count API")).toBeInTheDocument();
  });

  it("renders only allowlisted activity fields, never arbitrary event metadata", () => {
    render(
      <Dashboard
        data={data}
        error={null}
        loading={false}
        onNavigate={vi.fn()}
        onRefresh={vi.fn()}
      />,
    );

    expect(screen.getAllByText("brain.knowledge.proposed")).toHaveLength(1);
    expect(screen.queryByText("must-not-be-rendered")).not.toBeInTheDocument();
    expect(screen.queryByText("Bearer secret")).not.toBeInTheDocument();
    expect(screen.queryByText("Not shown")).not.toBeInTheDocument();
  });

  it("places the authorized graph between the operational widgets without mixing in the demo", () => {
    render(<Dashboard data={data} error={null} loading={false} onNavigate={vi.fn()} onRefresh={vi.fn()}
      graph={<div role="img" aria-label="Authorized live graph" />} />);
    expect(screen.getByRole("img", { name: "Authorized live graph" }).closest(".workspace-centre")).not.toBeNull();
    expect(screen.getByRole("complementary", { name: "Brain overview" })).toHaveTextContent("No document-count API");
    expect(screen.getByRole("complementary", { name: "Review and activity" })).toHaveTextContent("Operations/incident.md");
    expect(screen.queryByText("Studio North")).not.toBeInTheDocument();
  });
});
