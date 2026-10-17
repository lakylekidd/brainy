import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { AgentDetails, Role } from "../src/features/agents/api";
import type { GraphResult } from "../src/features/graph/api";

test("real identities, grant matrices, reviewed access controls and focused graph remain usable in both themes and mobile", async ({ page }) => {
  const line = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8")
    .split(/\r?\n/).find((value) => value.startsWith("BRAIN_UI_ADMIN_TOKEN="));
  if (!line) throw new Error("Run scripts\\Start-Development.ps1 before the identity browser checks.");
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.name));
  await page.goto("/");
  await page.getByLabel("Admin token").fill(line.slice("BRAIN_UI_ADMIN_TOKEN=".length));
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  const directoryResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/agents");
  await page.getByRole("button", { name: "Agents", exact: true }).click();
  const directory = await (await directoryResponse).json() as AgentDetails[];
  const target = directory.find((agent) => agent.id === "codex");
  expect(target).toBeDefined();
  await expect(page.getByRole("region", { name: "Agent list" })).toBeVisible();
  const detailResponse = page.waitForResponse((response) => new URL(response.url()).pathname === `/api/agents/${target!.id}/details`);
  await page.getByLabel("Inspect agent", { exact: true }).selectOption(target!.id);
  const detail = await (await detailResponse).json() as AgentDetails;
  const matrix = page.getByRole("region", { name: "Agent permission matrix" });
  await expect(matrix).toBeVisible();
  for (const grant of detail.permissions) {
    const row = matrix.getByRole("row").filter({ has: page.getByRole("rowheader", { name: grant, exact: true }) });
    await expect(row.getByRole("cell").last()).toHaveText("Granted");
  }
  await page.getByRole("button", { name: "Edit access", exact: true }).click();
  const form = page.getByRole("region", { name: "Edit access", exact: true });
  for (const grant of detail.permissions) {
    const checkbox = form.getByRole("checkbox", { name: grant, exact: true });
    if (detail.directPermissions.includes(grant)) await expect(checkbox).toBeChecked();
    else await expect(checkbox).not.toBeChecked();
  }
  await form.getByRole("checkbox", { name: "Agent enabled" }).uncheck();
  await form.getByRole("button", { name: "Review changes" }).click();
  await expect(form.getByRole("region", { name: "Review access changes" })).toContainText("authentication will be denied");
  await expect(form.getByRole("button", { name: "Confirm replacement" })).toBeVisible();
  await form.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(form).toHaveCount(0);
  const focusResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/graph/neighbors");
  await page.getByRole("button", { name: "Focus agent graph" }).click();
  const focus = await focusResponse;
  expect(focus.status()).toBe(200);
  expect(new URL(focus.url()).searchParams.get("id")).toBe(`agent:${detail.id}`);
  const graph = await focus.json() as GraphResult;
  expect(graph.entities.length).toBeLessThanOrEqual(80);
  expect(graph.relationships.every((edge) =>
    graph.entities.some((node) => node.id === edge.source) && graph.entities.some((node) => node.id === edge.target))).toBe(true);
  await expect(page.getByText("Focus graph", { exact: true })).toBeVisible();
  const rolesResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/roles");
  await page.getByRole("button", { name: "Permissions", exact: true }).click();
  const roles = await (await rolesResponse).json() as Role[];
  await page.getByLabel("Inspect role").selectOption(roles[0].id);
  await expect(page.getByRole("region", { name: "Roles and permission catalog" })).toContainText(roles[0].description);
  await expect(page.getByRole("region", { name: "Role permission matrix" })).toBeVisible();
  await page.getByRole("button", { name: /Switch to (dark|light) mode/ }).click();
  await expect(page.getByRole("region", { name: "Role permission matrix" })).toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole("button", { name: "Register agent", exact: true }).click();
  await expect(page.getByLabel("Agent ID", { exact: true })).toBeVisible();
  await expect(page.locator('input[type="password"]')).toHaveCount(0);
  await page.getByRole("region", { name: "Register agent", exact: true }).getByRole("button", { name: "Cancel" }).click();
  await page.route("**/api/roles", (route) => route.abort());
  await page.getByRole("button", { name: "Refresh directory" }).click();
  await expect(page.getByRole("alert")).toContainText("Check your connection");
  await page.getByRole("button", { name: "Open navigation" }).click();
  await page.getByRole("button", { name: "Dashboard", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);
  expect(errors).toEqual([]);
});
