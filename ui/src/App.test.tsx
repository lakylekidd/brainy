import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import App from "./App";

vi.mock("./features/pav/PavDashboard", () => ({
  default: () => <h1>PAV OS demo vault</h1>,
}));

describe("Brain UI shell", () => {
  it("authenticates, loads the dashboard, and keeps credentials only in tab session storage", async () => {
    const responses: Record<string, unknown> = {
      "/api/agents/me": { id: "codex", teamId: "platform", permissions: ["registry.read"] },
      "/health/ready": { status: "ready" },
      "/api/agents": [{ id: "codex", name: "Codex", enabled: true }],
      "/api/skills": [{ type: "skill", id: "triage", title: "Incident triage", content: "" }],
      "/api/tools": [],
      "/api/knowledge/proposals": [],
      "/api/knowledge/metadata/issues": [],
      "/api/audit": [],
      "/api/traces": [],
    };
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), window.location.origin);
      const matchingPath = Object.keys(responses).find((path) => url.pathname === path);
      return {
        ok: matchingPath !== undefined,
        status: matchingPath ? 200 : 404,
        json: async () => (matchingPath ? responses[matchingPath] : {}),
      } as Response;
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    expect(screen.getByText("BRAINY")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Switch to light mode" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Switch to light mode" }));
    fireEvent.click(screen.getByRole("button", { name: "Switch to dark mode" }));
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(screen.queryByLabelText("Agent ID")).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Admin token"), { target: { value: "private-token" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in to Brain" }));

    expect(await screen.findByText("Incident triage")).toBeInTheDocument();
    expect(screen.getByText("BRAINY")).toBeInTheDocument();
    expect(screen.getAllByText("codex")).toHaveLength(1);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      "/api/agents/me",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer private-token",
        }),
      }),
    ));
    expect(fetchMock.mock.calls[0]).toHaveLength(2);
    expect(Object.entries(localStorage)).toEqual([["brain-theme", "dark"]]);
    expect(sessionStorage.getItem("brain-ui-admin-token")).toBe("private-token");
    expect(screen.getByRole("button", { name: "Switch to light mode" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Knowledge graph" }));
    expect(await screen.findByRole("heading", { name: "Knowledge graph" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Dashboard" }));
    expect(await screen.findByRole("heading", { name: "PAV OS demo vault" })).toBeInTheDocument();

    fireEvent.click(screen.getAllByTitle("Sign out")[0]);
    expect(await screen.findByText("A clearer view of your Brain.")).toBeInTheDocument();
    expect(screen.queryByText("Incident triage")).not.toBeInTheDocument();
    expect(sessionStorage.getItem("brain-ui-admin-token")).toBeNull();
    expect(document.documentElement.dataset.theme).toBe("dark");
    fireEvent.click(screen.getByRole("button", { name: "Switch to light mode" }));
    expect(document.documentElement.dataset.theme).toBe("light");
  });
});
