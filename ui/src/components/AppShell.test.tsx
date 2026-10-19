import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import AppShell from "./AppShell";

afterEach(cleanup);
function shell(page: "pav" | "agents" = "pav") {
  const navigate = vi.fn();
  const signOut = vi.fn();
  render(<AppShell profile={{ id: "operator", teamId: "lab", permissions: [] }} page={page} theme="dark"
    onNavigate={navigate} onSignOut={signOut} onToggleTheme={vi.fn()}><h1>Workspace</h1></AppShell>);
  return { navigate, signOut };
}
describe("reference workspace shell", () => {
  it("keeps the dashboard full width and constrains other pages", () => {
    shell();
    expect(document.querySelector(".page-content-pav")).toBeInTheDocument();
    expect(document.querySelector(".page-content-constrained")).toBeNull();
    expect(screen.queryByRole("contentinfo")).not.toBeInTheDocument();
    cleanup();
    shell("agents");
    expect(document.querySelector(".page-content-constrained")).toBeInTheDocument();
    expect(document.querySelector(".page-content-pav")).toBeNull();
    expect(screen.getByRole("contentinfo")).toHaveTextContent(`© ${new Date().getFullYear()} Brainy contributors`);
    expect(screen.getByRole("contentinfo")).toHaveTextContent("PolyForm Noncommercial License 1.0.0");
  });
  it("preserves all routes in grouped compact navigation, with no permanent sidebar", () => {
    const { navigate, signOut } = shell();
    const nav = screen.getByRole("navigation", { name: "Main navigation" });
    expect(within(nav).getAllByRole("button")).toHaveLength(13);
    expect(within(nav).getByRole("button", { name: "Dashboard" })).toHaveAttribute("aria-current", "page");
    expect(document.querySelector(".sidebar")).toBeNull();
    fireEvent.click(within(nav).getByRole("button", { name: "Permissions" }));
    expect(navigate).toHaveBeenCalledWith("permissions");
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(signOut).toHaveBeenCalledOnce();
  });
  it("opens navigation on the active route and restores focus on Escape and selection", () => {
    const { navigate } = shell();
    fireEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    expect(screen.getByRole("button", { name: "Dashboard" })).toHaveFocus();
    expect(screen.getByRole("button", { name: "Close navigation" })).toHaveAttribute("aria-expanded", "true");
    fireEvent.keyDown(screen.getByRole("navigation"), { key: "Escape" });
    expect(screen.getByRole("button", { name: "Open navigation" })).toHaveFocus();
    fireEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    fireEvent.click(screen.getByRole("button", { name: "Agents" }));
    expect(navigate).toHaveBeenCalledWith("agents");
    expect(screen.getByRole("button", { name: "Open navigation" })).toHaveFocus();
  });
  it("keeps the focus strip collapsible and offers real Brain navigation, not invented priorities", () => {
    const { navigate } = shell();
    const focus = document.querySelector(".focus-strip")!;
    expect(focus).not.toHaveAttribute("open");
    fireEvent.click(focus.querySelector("summary")!);
    fireEvent.click(screen.getByRole("button", { name: /Explore knowledge/ }));
    expect(navigate).toHaveBeenCalledWith("graph");
    expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#workspace-content");
  });
});
