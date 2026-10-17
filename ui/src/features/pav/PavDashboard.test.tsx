import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import PavDashboard from "./PavDashboard";
import { VIEWS } from "./layouts";

vi.mock("./VaultCanvas", () => ({
  default: ({ graph, view, selected, onSelect, theme, showLinks, showLabels }: {
    graph: { files: { id: string }[]; links: unknown[] }; view: string; selected: string;
    onSelect: (id: string) => void; theme: string; showLinks: boolean; showLabels: boolean;
  }) => <div data-testid="vault-canvas" data-view={view} data-count={graph.files.length} data-theme={theme}
    data-links={String(showLinks)} data-labels={String(showLabels)} data-selected={selected}>
    <button onClick={() => onSelect("skills/research.md")}>Select research dot</button>
  </div>,
}));
afterEach(cleanup);

describe("PAV OS demo dashboard", () => {
  it("starts with a real vault, its entry point and all six switchable layouts", () => {
    render(<PavDashboard theme="dark" />);
    expect(screen.getByRole("heading", { name: /A map of your mind/ })).toBeInTheDocument();
    expect(screen.getByText("Demo vault / Read only")).toBeInTheDocument();
    expect(screen.getByText("Fictional workspace. Real Markdown files. No live Brain data.")).toBeInTheDocument();
    expect(screen.getByLabelText("Jump to file")).toHaveValue("AGENTS.md");
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "30");
    for (const view of VIEWS) {
      fireEvent.click(screen.getByRole("button", { name: view }));
      expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-view", view);
      expect(screen.getByRole("button", { name: view })).toHaveAttribute("aria-pressed", "true");
    }
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
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "4");
    fireEvent.change(screen.getByLabelText("Search demo files"), { target: { value: "atlas" } });
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "2");
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
    fireEvent.click(within(screen.getByRole("region", { name: "Vault areas" })).getByRole("button", { name: "Skills 4" }));
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-count", "5");
    expect(screen.getByLabelText("Jump to file")).toHaveValue("AGENTS.md");
    fireEvent.click(screen.getByRole("checkbox", { name: "Links" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Labels" }));
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-links", "false");
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-labels", "true");
    rerender(<PavDashboard theme="dark" />);
    expect(screen.getByTestId("vault-canvas")).toHaveAttribute("data-theme", "dark");
  });
});
