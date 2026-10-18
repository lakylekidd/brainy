import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Agents from "./Agents";
import { AgentDetails, Permission, Role } from "./api";

const credentials = { token: "test-private-token" };
const profile = { id: "operator", teamId: "platform", permissions: ["agents.read", "agents.admin"] };
const agent: AgentDetails = {
  id: "helper", name: "Helper", description: "An independent agent", teamId: "platform", enabled: true,
  roles: ["member"], directPermissions: ["memory.read"], permissions: ["memory.read", "memory.write"],
  createdAt: "2026-10-17T10:00:00Z", updatedAt: "2026-10-17T11:00:00Z",
};
const roles: Role[] = [
  { id: "member", description: "Scoped memory access", permissions: ["memory.read", "memory.write"] },
  { id: "unassigned", description: "No members yet", permissions: [] },
];
const permissions: Permission[] = [
  { id: "memory.read", description: "Read scoped memories" },
  { id: "memory.write", description: "Write own memories" },
  { id: "agents.admin", description: "Manage identities" },
];
const response = (value: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => value }) as Response;

function show(mode: "agents" | "permissions" = "agents", grants = profile.permissions) {
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation((input, options) => String(input) === "/api/agents/me"
    ? Promise.resolve(response({ ...profile, permissions: grants })) : original(input, options));
  const onUnauthorized = vi.fn(), onFocus = vi.fn();
  const result = render(<Agents credentials={credentials} profile={{ ...profile, permissions: grants }} mode={mode}
    onUnauthorized={onUnauthorized} onFocus={onFocus} />);
  return { ...result, onUnauthorized, onFocus };
}

async function inspect() {
  await screen.findByRole("option", { name: "Helper (helper) - Enabled" });
  fireEvent.change(screen.getByLabelText("Inspect agent"), { target: { value: "helper" } });
  return screen.findByRole("region", { name: "Agent permission matrix" });
}

function editAccess() {
  fireEvent.click(screen.getByRole("button", { name: "Edit access" }));
  return within(screen.getByRole("region", { name: "Edit access" }));
}

describe("agent identities and permission management", () => {
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, options?: RequestInit) => {
      const path = String(input);
      if (options?.method) return response({ ...agent, ...JSON.parse(String(options.body)) });
      if (path === "/api/agents") return response([agent]);
      if (path === "/api/roles") return response(roles);
      if (path === "/api/permissions") return response(permissions);
      return response({ ...agent, id: decodeURIComponent(path.split("/")[3]), metadata: { secret: "DO_NOT_RENDER" } });
    }));
  });

  it("loads independent agent details and separates direct, inherited and effective permissions without arbitrary metadata", async () => {
    const { onFocus } = show();
    const matrix = await inspect();
    expect(fetch).toHaveBeenCalledWith("/api/agents/helper/details", expect.anything());
    const read = within(matrix).getByRole("row", { name: "memory.read member Granted Granted" });
    expect(read).toBeInTheDocument();
    expect(within(matrix).getByRole("row", { name: "memory.write member Not granted Granted" })).toBeInTheDocument();
    expect(within(matrix).getByRole("row", { name: "agents.admin None Not granted Not granted" })).toBeInTheDocument();
    expect(screen.queryByText("DO_NOT_RENDER")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Focus agent graph" }));
    expect(onFocus).toHaveBeenCalledWith("agent:helper");
  });

  it("inspects unassigned roles and the role permission matrix", async () => {
    show("permissions");
    await screen.findByRole("region", { name: "Role permission matrix" });
    fireEvent.change(screen.getByLabelText("Inspect role"), { target: { value: "unassigned" } });
    expect(screen.getByText("No members yet")).toBeInTheDocument();
    const matrix = within(screen.getByRole("region", { name: "Role permission matrix" }));
    expect(matrix.getByRole("row", { name: "memory.read Read scoped memories Granted Not granted" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit role" })).not.toBeInTheDocument();
  });

  it("filters the returned directory and supports independent lookup outside the listed inventory", async () => {
    show();
    await screen.findByRole("button", { name: "Inspect helper" });
    fireEvent.change(screen.getByLabelText("Search agents"), { target: { value: "missing" } });
    expect(screen.getByText("No agents match this view.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Search agents"), { target: { value: "platform" } });
    expect(screen.getByRole("button", { name: "Inspect helper" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Agent status"), { target: { value: "disabled" } });
    expect(screen.getByText("No agents match this view.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Look up agent ID"), { target: { value: "outside-list" } });
    fireEvent.click(screen.getByRole("button", { name: "Inspect by ID" }));
    await screen.findByRole("region", { name: "Agent permission matrix" });
    expect(fetch).toHaveBeenCalledWith("/api/agents/outside-list/details", expect.anything());
  });

  it("requires read permission without requesting catalogs or offering management", () => {
    show("permissions", ["agents.admin"]);
    expect(screen.getByRole("alert")).toHaveTextContent("agents.read");
    expect(fetch).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Register agent" })).not.toBeInTheDocument();
  });

  it("keeps read-only inspection and focused graph available but hides unauthorized actions", async () => {
    show("agents", ["agents.read"]);
    await inspect();
    expect(screen.queryByRole("button", { name: "Edit access" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Register agent" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Focus agent graph" })).toBeEnabled();
  });

  it("prevents self-access changes in the UI", async () => {
    vi.mocked(fetch).mockImplementation(async (input) => {
      if (String(input) === "/api/agents") return response([{ ...agent, id: "operator" }]);
      if (String(input) === "/api/roles") return response(roles);
      if (String(input) === "/api/permissions") return response(permissions);
      return response({ ...agent, id: "operator" });
    });
    show();
    await screen.findByRole("option", { name: "Helper (operator) - Enabled" });
    fireEvent.change(screen.getByLabelText("Inspect agent"), { target: { value: "operator" } });
    await screen.findByText("Your own access cannot be changed through this endpoint.");
    expect(screen.queryByRole("button", { name: "Edit access" })).not.toBeInTheDocument();
  });

  it("initializes editing with direct grants only and explicitly confirms full access replacement", async () => {
    show();
    await inspect();
    const form = editAccess();
    expect(form.getByRole("checkbox", { name: "memory.read" })).toBeChecked();
    expect(form.getByRole("checkbox", { name: "memory.write" })).not.toBeChecked();
    fireEvent.click(form.getByRole("checkbox", { name: "member" }));
    fireEvent.click(form.getByRole("checkbox", { name: "Agent enabled" }));
    fireEvent.click(form.getByRole("button", { name: "Review changes" }));
    expect(fetch).not.toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ method: "PUT" }));
    expect(form.getByText(/This replaces all roles/)).toBeInTheDocument();
    fireEvent.click(form.getByRole("button", { name: "Confirm replacement" }));
    await screen.findByText("Saved Helper. Credentials remain operator-managed.");
    expect(fetch).toHaveBeenCalledWith("/api/agents/helper/access", expect.objectContaining({
      method: "PUT", body: JSON.stringify({ roles: [], permissions: ["memory.read"], enabled: false }),
      headers: expect.objectContaining({ "Content-Type": "application/json", Authorization: `Bearer ${credentials.token}` }),
    }));
  });

  it("registers an identity only after review without requesting or storing credentials", async () => {
    show();
    await screen.findByRole("button", { name: "Register agent" });
    fireEvent.click(screen.getByRole("button", { name: "Register agent" }));
    const form = within(screen.getByRole("region", { name: "Register agent" }));
    fireEvent.change(form.getByLabelText("Agent ID"), { target: { value: "new-agent" } });
    fireEvent.change(form.getByLabelText("Name"), { target: { value: "New Agent" } });
    fireEvent.click(form.getByRole("checkbox", { name: "member" }));
    fireEvent.click(form.getByRole("button", { name: "Review changes" }));
    expect(form.getByText(/without a credential/)).toBeInTheDocument();
    fireEvent.click(form.getByRole("button", { name: "Confirm registration" }));
    await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/agents", expect.objectContaining({
      method: "POST", body: JSON.stringify({ id: "new-agent", name: "New Agent", description: "", teamId: null, roles: ["member"], permissions: [] }),
    })));
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it.each([400, 404, 409, 503])("shows an explicit failed mutation (%s), never success or server diagnostics", async (status) => {
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, options) =>
      options?.method ? response({ detail: "DO_NOT_RENDER" }, status) : original(input, options));
    show();
    await inspect();
    const form = editAccess();
    fireEvent.click(form.getByRole("button", { name: "Review changes" }));
    fireEvent.click(form.getByRole("button", { name: "Confirm replacement" }));
    await screen.findByRole("alert");
    expect(screen.queryByText(/Saved Helper/)).not.toBeInTheDocument();
    expect(screen.queryByText("DO_NOT_RENDER")).not.toBeInTheDocument();
  });

  it.each([401, 403])("handles revoked management access (%s) and removes stale details and forms", async (status) => {
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, options) =>
      options?.method ? response({}, status) : original(input, options));
    const { onUnauthorized } = show();
    await inspect();
    const form = editAccess();
    fireEvent.click(form.getByRole("button", { name: "Review changes" }));
    fireEvent.click(form.getByRole("button", { name: "Confirm replacement" }));
    await waitFor(() => expect(screen.queryByRole("region", { name: "Edit access" })).not.toBeInTheDocument());
    expect(screen.queryByRole("region", { name: "Agent details" })).not.toBeInTheDocument();
    if (status === 401) expect(onUnauthorized).toHaveBeenCalledOnce();
    else expect(screen.getByRole("alert")).toHaveTextContent("management access may have changed");
  });

  it.each([403, 503, 0])("keeps the application usable on denied, unavailable or offline catalogs (%s)", async (status) => {
    vi.stubGlobal("fetch", vi.fn(async () => {
      if (status === 0) throw new TypeError("offline");
      return response({}, status);
    }));
    show();
    await screen.findByRole("alert");
    expect(screen.getByRole("button", { name: "Refresh directory" })).toBeEnabled();
    expect(screen.queryByRole("region", { name: "Agent directory" })).not.toBeInTheDocument();
  });

  it("reports an empty directory, roles and permissions explicitly", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => response([])));
    show("permissions");
    await screen.findByText("No agents match this view.");
    expect(screen.getByText("No roles configured.")).toBeInTheDocument();
    expect(screen.getByText("No permissions configured.")).toBeInTheDocument();
  });

  it("re-resolves current grants on refresh and hides management after revocation", async () => {
    show();
    await screen.findByRole("button", { name: "Register agent" });
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, options) => String(input) === "/api/agents/me"
      ? Promise.resolve(response({ ...profile, permissions: ["agents.read"] })) : original(input, options));
    fireEvent.click(screen.getByRole("button", { name: "Refresh directory" }));
    await screen.findByRole("button", { name: "Inspect helper" });
    expect(screen.queryByRole("button", { name: "Register agent" })).not.toBeInTheDocument();
    await inspect();
    expect(screen.queryByRole("button", { name: "Edit access" })).not.toBeInTheDocument();
  });

  it.each([403, 404])("clears unavailable independent details instead of retaining access controls (%s)", async (status) => {
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, options) => String(input) === "/api/agents/helper/details"
      ? Promise.resolve(response({}, status)) : original(input, options));
    show();
    await screen.findByRole("button", { name: "Inspect helper" });
    fireEvent.click(screen.getByRole("button", { name: "Inspect helper" }));
    await screen.findByRole("alert");
    expect(screen.queryByRole("button", { name: "Edit access" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Focus agent graph" })).not.toBeInTheDocument();
  });

  it("clears authentication on expired directory credentials", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => response({}, 401)));
    const { onUnauthorized } = show();
    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledOnce());
    expect(screen.queryByRole("region", { name: "Agent directory" })).not.toBeInTheDocument();
  });

  it("does not allow a stale agent detail response to replace a newer selection", async () => {
    let finish: ((value: Response) => void) | undefined;
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, options) => {
      if (String(input) === "/api/agents/helper/details") return new Promise<Response>((resolve) => { finish = resolve; });
      if (String(input) === "/api/agents/second/details") return response({ ...agent, id: "second", name: "Second" });
      return original(input, options);
    });
    show();
    await screen.findByRole("button", { name: "Inspect helper" });
    fireEvent.click(screen.getByRole("button", { name: "Inspect helper" }));
    await waitFor(() => expect(finish).toBeDefined());
    fireEvent.change(screen.getByLabelText("Look up agent ID"), { target: { value: "second" } });
    fireEvent.click(screen.getByRole("button", { name: "Inspect by ID" }));
    await screen.findByRole("heading", { name: "Second" });
    await act(async () => finish?.(response(agent)));
    expect(screen.queryByRole("heading", { name: "Helper" })).not.toBeInTheDocument();
  });
});
