import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import PavDashboard from "./PavDashboard";
import { VIEWS } from "./layouts";
import { filterVault, loadDemoVault } from "./vault";

const graph = loadDemoVault();

vi.mock("./VaultCanvas", () => ({
  default: ({ graph, view, selected, onSelect, onInspect, theme, showLinks, showLabels, dotSize }: {
    graph: { files: { id: string }[]; links: unknown[] }; view: string; selected: string;
    onSelect: (id: string) => void; onInspect: () => void; theme: string; showLinks: boolean; showLabels: boolean; dotSize: number;
  }) => <div data-testid="vault-canvas" data-view={view} data-count={graph.files.length} data-theme={theme}
    data-links={String(showLinks)} data-labels={String(showLabels)} data-selected={selected} data-dot-size={dotSize}>
    <button onClick={() => { onSelect("skills/research.md"); onInspect(); }}>Select research dot</button>
    <button onClick={() => { onSelect("content/index.md"); onInspect(); }}>Select content hub</button>
  </div>,
}));
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("PAV OS demo dashboard", () => {
  it("starts with a real vault, its entry point and all six switchable layouts", () => {
    render(<PavDashboard theme="dark" />);
    expect(screen.getByRole("heading", { name: "PAV OS Agentic OS" })).toBeInTheDocument();
    expect(screen.getByText(/Demo vault \/ Read only/)).toBeInTheDocument();
    expect(screen.getByText("Fictional workspace. Real Markdown files. No live Brain data.")).toBeInTheDocument();
    expect(screen.getByLabelText("Jump to file")).toHaveValue("AGENTS.md");
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", String(graph.files.length));
    expect(screen.queryByRole("complementary", { name: "Demo file details" })).not.toBeInTheDocument();
    expect(screen.queryByText("IN YOUR ORBIT")).not.toBeInTheDocument();
    expect(screen.getByRole("complementary", { name: "Demo vault overview" })).toHaveTextContent(String(graph.files.length));
    expect(screen.getByRole("complementary", { name: "Demo routines and context" })).toHaveTextContent("Not scheduled");
    for (const view of VIEWS) {
      fireEvent.click(screen.getByRole("button", { name: view }));
      expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-view", view);
      expect(screen.getByRole("button", { name: view })).toHaveAttribute("aria-pressed", "true");
    }
  });

  it("inspects a real initialed hub and distinguishes its area membership from Markdown connections", () => {
    render(<PavDashboard theme="dark" />);
    fireEvent.click(screen.getByRole("button", { name: "Select content hub" }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("content/index.md");
    const inspector = screen.getByRole("complementary", { name: "Demo file details" });
    expect(inspector).toHaveTextContent("24 real files in this area");
    expect(inspector).toHaveTextContent("Dashed spokes show membership");
    const members = within(inspector).getByRole("region", { name: "Files in this area" });
    fireEvent.click(within(members).getByRole("button", { name: /Clear writing/ }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("skills/writing.md");
    expect(inspector).toHaveTextContent("Content / Skill / MARKDOWN");
  });

  it("uses the reference-style skill deck and routine widgets to inspect only actual demo files", () => {
    render(<PavDashboard theme="dark" />);
    fireEvent.click(within(screen.getByRole("complementary", { name: "Demo routines and context" })).getByRole("button", { name: "Weekly review" }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("routines/weekly-review.md");
    fireEvent.click(screen.getByRole("button", { name: /Research synthesis skills\/research.md/ }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("skills/research.md");
    expect(screen.getByRole("complementary", { name: "Demo file details" })).toHaveTextContent("Research synthesis");
  });

  it("keeps file details dismissible without losing selection and adjusts the reference dot size", () => {
    render(<PavDashboard theme="dark" />);
    expect(screen.getByRole("slider", { name: "Dot size" })).toHaveValue("85");
    fireEvent.change(screen.getByRole("slider", { name: "Dot size" }), { target: { value: "125" } });
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-dot-size", "125");
    fireEvent.change(screen.getByLabelText("Jump to file"), { target: { value: "skills/research.md" } });
    expect(screen.getByRole("button", { name: "File details" })).toHaveAttribute("aria-expanded", "true");
    fireEvent.click(screen.getByRole("button", { name: "Close file details" }));
    expect(screen.getByRole("button", { name: "File details" })).toHaveFocus();
    expect(screen.queryByRole("complementary", { name: "Demo file details" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Jump to file")).toHaveValue("skills/research.md");
    fireEvent.click(screen.getByRole("button", { name: "File details" }));
    fireEvent.keyDown(screen.getByRole("region", { name: "Demo vault exploration" }), { key: "Escape" });
    expect(screen.getByRole("button", { name: "File details" })).toHaveAttribute("aria-expanded", "false");
  });

  it("reports rejected fullscreen requests rather than pretending to enter fullscreen", async () => {
    const warning = vi.spyOn(console, "warn").mockImplementation(() => {});
    render(<PavDashboard theme="dark" />);
    const map = screen.getByRole("region", { name: "Demo vault exploration" });
    Object.assign(map, { requestFullscreen: vi.fn().mockRejectedValue(new Error("Denied")) });
    fireEvent.click(screen.getByRole("button", { name: "Enter map fullscreen" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("could not change fullscreen mode");
    expect(warning).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Enter map fullscreen" })).toHaveAttribute("aria-pressed", "false");
  });

  it("inspects a selected dot, its real source body and incoming/outgoing links", () => {
    render(<PavDashboard theme="light" />);
    fireEvent.click(screen.getByRole("button", { name: "Select research dot" }));
    const inspector = screen.getByRole("complementary", { name: "Demo file details" });
    expect(within(inspector).getByRole("heading", { name: "Research synthesis" })).toBeInTheDocument();
    expect(within(inspector).getByText("skills/research.md", { selector: "code" })).toBeInTheDocument();
    fireEvent.click(screen.getByText("Read document"));
    expect(within(inspector).getByText(/Separate observations from interpretations/, { selector: "pre" })).toBeInTheDocument();
    fireEvent.click(within(inspector).getByRole("button", { name: /Atlas website/ }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("business/atlas-project.md");
    fireEvent.click(screen.getByRole("button", { name: "Back to AGENTS.md" }));
    expect(screen.getByLabelText("Jump to file")).toHaveValue("AGENTS.md");
  });

  it("filters files, keeps the root, and explicitly reports empty results", () => {
    render(<PavDashboard theme="light" />);
    fireEvent.change(screen.getByLabelText("Filter demo area"), { target: { value: "business" } });
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", String(filterVault(graph, "", "business").files.length));
    fireEvent.change(screen.getByLabelText("Search demo files"), { target: { value: "atlas" } });
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", String(filterVault(graph, "atlas", "business").files.length));
    fireEvent.change(screen.getByLabelText("Search demo files"), { target: { value: "unknown-file" } });
    expect(screen.getByRole("status")).toHaveTextContent("No matching files");
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "1");
    fireEvent.change(screen.getByLabelText("Jump to file"), { target: { value: "skills/writing.md" } });
    expect(screen.getByLabelText("Filter demo area")).toHaveValue("all");
    expect(screen.getByLabelText("Search demo files")).toHaveValue("");
    expect(screen.getByLabelText("Jump to file")).toHaveValue("skills/writing.md");
  });

  it("applies legend filters, falls back to the visible root, toggles links and labels and follows theme changes", () => {
    const { rerender } = render(<PavDashboard theme="light" />);
    fireEvent.change(screen.getByLabelText("Jump to file"), { target: { value: "business/atlas-project.md" } });
    const region = within(screen.getByRole("region", { name: "Vault areas" }));
    fireEvent.click(region.getByRole("button", { name: "Content 24" }));
    fireEvent.change(screen.getByLabelText("Filter file type"), { target: { value: "skill" } });
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "3");
    expect(screen.getByLabelText("Jump to file")).toHaveValue("AGENTS.md");
    fireEvent.click(screen.getByRole("checkbox", { name: "Links" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Labels" }));
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-links", "false");
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-labels", "true");
    rerender(<PavDashboard theme="dark" />);
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-theme", "dark");
  });
});
