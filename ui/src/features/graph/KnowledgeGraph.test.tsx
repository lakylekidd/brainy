import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import KnowledgeGraph from "./KnowledgeGraph";
import { GraphEntity, GraphResult } from "./api";

vi.mock("./GraphCanvas", () => ({
  default: ({ data }: { data: GraphResult }) => <div role="img" aria-label={`Graph with ${data.entities.length} nodes and ${data.relationships.length} edges`} />,
}));

const credentials = { token: "test-only-token" };
const root: GraphEntity = {
  id: "document:root.md", type: "document", label: "Root document",
  source: { kind: "knowledge_document", id: "root.md" }, metadata: { path: "root.md", indexedAt: "2026-10-17" },
};
const tool: GraphEntity = {
  id: "tool:logs", type: "tool", label: "Log explorer", source: { kind: "registry_manifest", id: "tools/logs/manifest.yaml" },
  metadata: { provider: "mcp" },
};
const edge = { id: "edge:root-logs", type: "references", source: root.id, target: tool.id };
const graph = (entities: GraphEntity[], relationships: GraphResult["relationships"] = [], truncated = false): GraphResult =>
  ({ entities, relationships, truncated, depth: 0 });
const response = (value: unknown, status = 200) => ({ ok: status === 200, status, json: async () => value }) as Response;
function renderGraph() {
  const onUnauthorized = vi.fn();
  render(<KnowledgeGraph credentials={credentials} theme="light" onUnauthorized={onUnauthorized} />);
  return onUnauthorized;
}

describe("knowledge graph exploration", () => {
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.unstubAllEnvs(); });
  beforeEach(() => {
    vi.stubEnv("VITE_WEB_OBSIDIAN_BASE_URL", "");
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), "http://localhost");
      if (url.pathname === "/api/graph/entity")
        return response({ ...(url.searchParams.get("id") === root.id ? root : tool), body: "DO_NOT_RENDER", metadata: {
          ...(url.searchParams.get("id") === root.id ? root.metadata : tool.metadata), credential: "DO_NOT_RENDER",
        } });
      if (url.pathname === "/api/graph/neighbors") return response(graph([root, tool], [edge]));
      return response(graph(url.searchParams.get("types")?.includes("document") ? [root] : [tool]));
    }));
  });

  it("links only inspected documents using the canonical source, never a metadata destination", async () => {
    vi.stubEnv("VITE_WEB_OBSIDIAN_BASE_URL", "https://obsidian.example");
    const document = { ...root, source: { ...root.source, id: "Projects/My note #1.md" },
      metadata: { path: "https://evil.example/private.md", url: "https://evil.example" } };
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), "http://localhost");
      if (url.pathname === "/api/graph/entity")
        return response(url.searchParams.get("id") === root.id ? document : tool);
      return response(graph([root, tool]));
    }));
    renderGraph();
    await screen.findByRole("option", { name: "Root document (document)" });
    expect(screen.queryByRole("link", { name: /Open in WebObsidian/ })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: root.id } });
    const link = await screen.findByRole("link", { name: /Open in WebObsidian/ });
    expect(link).toHaveAttribute("href", "https://obsidian.example/note/Projects/My%20note%20%231.md");
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
    expect(link).toHaveAttribute("referrerpolicy", "no-referrer");
    expect(screen.queryByText(/evil\.example/)).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: tool.id } });
    await screen.findByRole("heading", { name: "Log explorer" });
    expect(screen.queryByRole("link", { name: /Open in WebObsidian/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/WebObsidianBaseUrl/)).not.toBeInTheDocument();
  });

  it.each([
    ["", "not configured"],
    ["javascript:alert(1)", "WebObsidianBaseUrl is invalid"],
  ])("keeps inspection usable with unavailable configuration (%s)", async (base, message) => {
    vi.stubEnv("VITE_WEB_OBSIDIAN_BASE_URL", base);
    renderGraph();
    await screen.findByRole("option", { name: "Root document (document)" });
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: root.id } });
    expect(await screen.findByText(new RegExp(message))).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Open in WebObsidian/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Focus entity" })).toBeEnabled();
  });

  it("loads bounded nodes, inspects one entity independently, and progressively expands relationships", async () => {
    renderGraph();
    expect(screen.getByRole("status")).toHaveTextContent("Loading graph");
    await screen.findByRole("option", { name: "Root document (document)" });
    expect(screen.getByRole("img")).toHaveAccessibleName("Graph with 1 nodes and 0 edges");
    expect(document.querySelector(".graph-entity-list")).toBeNull();
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: root.id } });
    expect(await screen.findByRole("heading", { name: "Root document" })).toBeInTheDocument();
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining("/api/graph/entity?id=document%3Aroot.md"), expect.anything());
    expect(screen.queryByText("DO_NOT_RENDER")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Expand neighbors" }));
    expect(await screen.findByRole("option", { name: "Log explorer (tool)" })).toBeInTheDocument();
    expect(screen.getByRole("img")).toHaveAccessibleName("Graph with 2 nodes and 1 edges");
    expect(screen.getByText("Outgoing · references")).toBeInTheDocument();
    fireEvent.click(within(screen.getByRole("complementary", { name: "Entity details" })).getByRole("button", { name: "Log explorer" }));
    expect(await screen.findByRole("heading", { name: "Log explorer" })).toBeInTheDocument();
    expect(screen.getByText("mcp", { exact: true })).toBeInTheDocument();
  });

  it("uses server filters and search rather than downloading the full graph", async () => {
    renderGraph();
    await screen.findByRole("option", { name: "Root document (document)" });
    fireEvent.click(screen.getByRole("checkbox", { name: "document" }));
    expect(await screen.findByRole("option", { name: "Log explorer (tool)" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Root document (document)" })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Search graph"), { target: { value: "logs" } });
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    await waitFor(() => expect(fetch).toHaveBeenCalledWith(expect.stringContaining("/api/graph/search?"), expect.anything()));
    const urls = vi.mocked(fetch).mock.calls.map(([url]) => new URL(String(url), "http://localhost"));
    const last = urls[urls.length - 1];
    expect(last.searchParams.get("types")).not.toContain("document");
    expect(last.searchParams.get("query")).toBe("logs");
    expect(last.searchParams.get("limit")).toBe("80");
    expect(screen.getByText("Search: logs")).toBeInTheDocument();
  });

  it("focuses the selected entity with bounded depth and can return to the global overview", async () => {
    renderGraph();
    await screen.findByRole("option", { name: "Root document (document)" });
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: root.id } });
    await screen.findByRole("heading", { name: "Root document" });
    fireEvent.click(screen.getByRole("button", { name: "Focus entity" }));
    expect(await screen.findByText("Focus graph", { exact: true })).toBeInTheDocument();
    await waitFor(() => expect(fetch).toHaveBeenCalledWith(expect.stringContaining("depth=1"), expect.anything()));
    fireEvent.change(screen.getByLabelText("Relationship depth"), { target: { value: "2" } });
    await waitFor(() => expect(fetch).toHaveBeenCalledWith(expect.stringContaining("depth=2"), expect.anything()));
    fireEvent.click(screen.getByRole("button", { name: "Global graph" }));
    expect(await screen.findByText("Global overview", { exact: true })).toBeInTheDocument();
    expect(screen.queryByLabelText("Relationship depth")).not.toBeInTheDocument();
  });

  it.each([403, 422, 503])("reports graph errors (%i) without disabling controls", async (status) => {
    vi.stubGlobal("fetch", vi.fn(async () => response({}, status)));
    const unauthorized = renderGraph();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry graph" })).toBeEnabled();
    expect(screen.getByLabelText("Search graph")).toBeEnabled();
    expect(unauthorized).not.toHaveBeenCalled();
  });

  it("shows empty results and an explicit bounded view notice", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => response(graph([]))));
    renderGraph();
    expect(await screen.findByRole("heading", { name: "No accessible entities found" })).toBeInTheDocument();
    vi.mocked(fetch).mockResolvedValue(response(graph([root], [], true)));
    fireEvent.click(screen.getByRole("button", { name: "Refresh graph" }));
    expect(await screen.findByText(/This is a bounded view/)).toBeInTheDocument();
  });

  it("clears unavailable detail and removes its loaded node instead of retaining stale access", async () => {
    vi.stubEnv("VITE_WEB_OBSIDIAN_BASE_URL", "https://obsidian.example");
    renderGraph();
    await screen.findByRole("option", { name: "Root document (document)" });
    vi.mocked(fetch).mockResolvedValue(response({}, 404));
    fireEvent.change(screen.getByLabelText("Inspect entity"), { target: { value: root.id } });
    expect(await screen.findByRole("alert")).toHaveTextContent("no longer available or accessible");
    expect(screen.queryByRole("option", { name: "Root document (document)" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Open in WebObsidian/ })).not.toBeInTheDocument();
  });

  it("expires authentication instead of treating a 401 as an empty graph", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => response({}, 401)));
    const unauthorized = renderGraph();
    await waitFor(() => expect(unauthorized).toHaveBeenCalledOnce());
    expect(screen.queryByRole("heading", { name: "No accessible entities found" })).not.toBeInTheDocument();
  });

  it("ignores old results when a filter changes during a request", async () => {
    let resolveOld: (value: Response) => void = () => undefined;
    vi.stubGlobal("fetch", vi.fn().mockImplementationOnce(() => new Promise<Response>((resolve) => { resolveOld = resolve; }))
      .mockResolvedValue(response(graph([tool]))));
    renderGraph();
    fireEvent.click(screen.getByRole("checkbox", { name: "document" }));
    expect(await screen.findByRole("option", { name: "Log explorer (tool)" })).toBeInTheDocument();
    await act(async () => resolveOld(response(graph([root]))));
    expect(screen.queryByRole("option", { name: "Root document (document)" })).not.toBeInTheDocument();
  });
});
