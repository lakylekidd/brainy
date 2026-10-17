import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

test("token-only login loads real dashboard data and sign-out clears access", async ({ page, request }) => {
  const lines = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8").split(/\r?\n/);
  const tokenLine = lines.find((line) => line.startsWith("BRAIN_UI_ADMIN_TOKEN="));
  if (!tokenLine) throw new Error("Run scripts\\Start-Development.ps1 before the end-to-end test.");
  const token = tokenLine.slice("BRAIN_UI_ADMIN_TOKEN=".length);
  const expectedId = lines.find((line) => line.startsWith("BRAIN_UI_ADMIN_ID="))?.slice("BRAIN_UI_ADMIN_ID=".length)
    ?? "willem-de-jong";
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.name));

  expect((await request.get("/api/agents/me")).status()).toBe(401);
  await page.goto("/");
  await expect(page.getByLabel("Admin token")).toBeVisible();
  await expect(page.getByLabel("Agent ID")).toHaveCount(0);
  await expect(page.locator('input[type="password"]')).toHaveCount(1);
  await page.emulateMedia({ colorScheme: "light" });
  await page.getByRole("button", { name: "Switch to dark mode" }).click();

  await page.getByLabel("Admin token").fill("not-a-valid-admin-token-0000000000000000");
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("alert")).toContainText("Could not sign in");
  await expect(page.getByRole("heading", { name: "Good to see you." })).toHaveCount(0);

  const responses = new Map<string, number>();
  page.on("response", (response) => {
    const path = new URL(response.url()).pathname;
    if (path.startsWith("/api/") || path === "/health/ready") responses.set(path, response.status());
  });
  await page.getByLabel("Admin token").fill(token);
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  await expect(page.getByText("Brain API ready", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Refresh" })).toBeEnabled();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.locator(".panel").first()).toHaveCSS("background-color", "rgb(23, 34, 48)");
  await expect(page.locator(".metric-card").first()).toHaveCSS("background-color", "rgb(23, 34, 48)");
  for (const path of [
    "/api/agents/me", "/health/ready", "/api/agents", "/api/skills", "/api/tools",
    "/api/knowledge/proposals", "/api/knowledge/metadata/issues", "/api/audit", "/api/traces",
  ]) {
    expect(responses.get(path), path).toBe(200);
  }
  await expect(page.getByText(expectedId, { exact: true })).toHaveCount(2);
  expect(await page.evaluate(() => ({ local: Object.entries(localStorage), session: sessionStorage.length })))
    .toEqual({ local: [["brain-theme", "dark"]], session: 0 });
  await page.getByRole("button", { name: "Knowledge graph", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Knowledge graph" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  await page.getByRole("button", { name: "Dashboard", exact: true }).click();
  await expect(page.locator(".panel").first()).toHaveCSS("background-color", "rgb(255, 255, 255)");
  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole("button", { name: "Switch to dark mode" }).click();
  await expect(page.getByRole("button", { name: "Switch to light mode" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(page.getByRole("navigation", { name: "Main navigation" })).toBeVisible();
  await page.getByRole("button", { name: "Close navigation" }).last().click();
  await page.setViewportSize({ width: 1280, height: 720 });
  expect(errors).toEqual([]);
  await page.getByTitle("Sign out").first().click();
  await expect(page.getByLabel("Admin token")).toHaveValue("");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Good to see you." })).toHaveCount(0);
});
