import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { GraphEntity, GraphResult } from "../src/features/graph/api";

function webObsidianBaseUrl() {
  const line = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8")
    .split(/\r?\n/).find((value) => value.startsWith("BRAIN_WEB_OBSIDIAN_BASE_URL="));
  return process.env.BRAIN_WEB_OBSIDIAN_BASE_URL ?? line?.slice("BRAIN_WEB_OBSIDIAN_BASE_URL=".length);
}

function adminToken() {
  const line = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8")
    .split(/\r?\n/).find((value) => value.startsWith("BRAIN_UI_ADMIN_TOKEN="));
  if (!line) throw new Error("Run scripts\\Start-Development.ps1 before the graph browser checks.");
  return line.slice("BRAIN_UI_ADMIN_TOKEN=".length);
}

test("real graph renders nodes and edges, inspects, expands, filters and focuses without loading all sources", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.name));
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  const initialResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/graph");
  await page.getByRole("button", { name: "Knowledge graph", exact: true }).click();
  const response = await initialResponse;
  expect(response.status()).toBe(200);
  const initial = await response.json() as GraphResult;
  expect(initial.entities.length).toBeGreaterThan(0);
  expect(initial.entities.length).toBeLessThanOrEqual(80);
  expect(initial.relationships.length).toBeGreaterThan(0);
  const dependency = initial.relationships.find((edge) => edge.type === "requires_tool");
  expect(dependency).toBeDefined();
  const selected = initial.entities.find((entity) => entity.id === dependency!.source)!;
  await expect(page.locator(".graph-canvas canvas").first()).toBeVisible();
  await expect(page.getByText(/Graph visualization is unavailable/)).toHaveCount(0);
  await expect(page.getByRole("img", { name: /^Knowledge graph:/ })).toBeVisible();
  await expect(page.locator(".graph-entity-list")).toHaveCount(0);
  const canvasBox = await page.locator(".graph-canvas").boundingBox();
  const explorerBox = await page.getByRole("region", { name: "Graph exploration" }).boundingBox();
  const inspectorBox = await page.getByRole("complementary", { name: "Entity details" }).boundingBox();
  const contentBox = await page.locator(".knowledge-graph").boundingBox();
  expect(canvasBox!.width).toBeGreaterThan(contentBox!.width * 0.98);
  expect(canvasBox!.width).toBeGreaterThan(1540);
  expect(canvasBox!.height).toBeGreaterThanOrEqual(480);
  expect(canvasBox!.height).toBeGreaterThanOrEqual(page.viewportSize()!.height * 0.7);
  expect(inspectorBox!.y).toBeGreaterThanOrEqual(explorerBox!.y + explorerBox!.height);
  const detailResponse = page.waitForResponse((item) => new URL(item.url()).pathname === "/api/graph/entity");
  await page.getByRole("combobox", { name: "Inspect entity", exact: true }).selectOption(selected.id);
  expect((await detailResponse).status()).toBe(200);
  await expect(page.getByRole("complementary", { name: "Entity details" }).getByRole("heading", { name: selected.label, exact: true })).toBeVisible();
  const expansion = page.waitForResponse((item) => new URL(item.url()).pathname === "/api/graph/neighbors");
  await page.getByRole("button", { name: "Expand neighbors" }).click();
  const expanded = await expansion;
  expect(expanded.status()).toBe(200);
  expect(new URL(expanded.url()).searchParams.get("depth")).toBe("1");
  const focused = page.waitForResponse((item) => new URL(item.url()).pathname === "/api/graph/neighbors");
  await page.getByRole("button", { name: "Focus entity" }).click();
  expect((await focused).status()).toBe(200);
  await expect(page.getByText("Focus graph", { exact: true })).toBeVisible();
  const twoHops = page.waitForResponse((item) => new URL(item.url()).searchParams.get("depth") === "2");
  await page.getByLabel("Relationship depth").selectOption("2");
  expect((await twoHops).status()).toBe(200);
  await expect(page.getByRole("button", { name: "Focus entity" })).toBeEnabled();
  await page.getByRole("button", { name: "Global graph", exact: true }).click();
  await expect(page.getByText("Global overview", { exact: true })).toBeVisible();
  const searchResponse = page.waitForResponse((item) => new URL(item.url()).pathname === "/api/graph/search");
  await page.getByLabel("Search graph").fill(selected.label);
  await page.getByRole("button", { name: "Search", exact: true }).click();
  const search = await searchResponse;
  expect(search.status()).toBe(200);
  const found = await search.json() as GraphResult;
  expect(found.entities.some((entity) => entity.id === selected.id)).toBe(true);
  const filterResponse = page.waitForResponse((item) => new URL(item.url()).pathname === "/api/graph/search");
  await page.getByRole("checkbox", { name: "document", exact: true }).uncheck();
  const filtered = await (await filterResponse).json() as GraphResult;
  expect(filtered.entities.every((entity) => entity.type !== "document")).toBe(true);
  for (const entity of [...initial.entities, ...found.entities, ...filtered.entities]) {
    expect(Object.keys(entity).sort()).toEqual(["id", "label", "metadata", "source", "type"]);
    expect(Object.keys(entity.metadata).every((key) => ["path", "indexedAt", "enabled", "provider"].includes(key))).toBe(true);
  }
  const themeButton = page.getByRole("button", { name: /Switch to (light|dark) mode/ });
  await themeButton.click();
  await expect(page.locator(".graph-canvas canvas").first()).toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByLabel("Search graph")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const mobileCanvas = await page.locator(".graph-canvas").boundingBox();
  expect(mobileCanvas!.height).toBeGreaterThanOrEqual(Math.floor(812 * 0.7));
  expect(await page.evaluate(() => {
    const canvas = document.querySelector(".graph-canvas")!.getBoundingClientRect();
    const inspector = document.querySelector(".graph-details")!.getBoundingClientRect();
    return inspector.top >= canvas.bottom;
  })).toBe(true);
  await page.route(/\/api\/graph(?:\/|\?)/, (route) => route.abort());
  await page.getByRole("button", { name: "Refresh graph" }).click();
  await expect(page.getByRole("alert")).toContainText("Check your connection");
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.getByRole("button", { name: "Dashboard", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  expect(errors).toEqual([]);
});

test("browsers without WebGL retain keyboard exploration and source details", async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(window, "WebGL2RenderingContext", { value: undefined }));
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  const initialResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/graph");
  await page.getByRole("button", { name: "Knowledge graph", exact: true }).click();
  const initial = await (await initialResponse).json() as GraphResult;
  await expect(page.getByText(/Graph visualization is unavailable/)).toBeVisible();
  const entity: GraphEntity = initial.entities[0];
  await page.getByRole("combobox", { name: "Inspect entity", exact: true }).focus();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("combobox", { name: "Inspect entity", exact: true })).toHaveValue(entity.id);
  await expect(page.getByRole("complementary", { name: "Entity details" }).getByRole("heading", { name: entity.label, exact: true })).toBeVisible();
});

test("canonical document inspection links to the configured WebObsidian note without sending Brain credentials", async ({ page, context }) => {
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  const initialResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/graph");
  await page.getByRole("button", { name: "Knowledge graph", exact: true }).click();
  const initial = await (await initialResponse).json() as GraphResult;
  const entity = initial.entities.find((item) => item.type === "document");
  expect(entity, "Index the development vault before running this browser check.").toBeDefined();
  await page.getByRole("combobox", { name: "Inspect entity", exact: true }).selectOption(entity!.id);
  await expect(page.getByRole("complementary", { name: "Entity details" })
    .getByRole("heading", { name: entity!.label, exact: true })).toBeVisible();
  const base = webObsidianBaseUrl();
  if (!base) {
    await expect(page.getByRole("status")).toContainText("WebObsidian is not configured");
    await expect(page.getByRole("link", { name: /Open in WebObsidian/ })).toHaveCount(0);
    return;
  }
  const url = new URL(base);
  const expected = `${url.origin}${url.pathname.replace(/\/+$/, "")}/note/${entity!.source.id.split("/").map(encodeURIComponent).join("/")}`;
  const link = page.getByRole("link", { name: /Open in WebObsidian/ });
  await expect(link).toHaveAttribute("href", expected);
  await expect(link).toHaveAttribute("rel", "noopener noreferrer");
  // Intercept only the external navigation; Brain API/data remain real.
  await context.route(expected, async (route) => {
    const headers = route.request().headers();
    expect(headers.authorization).toBeUndefined();
    expect(headers["x-agent-id"]).toBeUndefined();
    expect(headers.referer).toBeUndefined();
    await route.fulfill({ contentType: "text/html", body: "<title>WebObsidian navigation check</title>" });
  });
  await link.focus();
  const popup = page.waitForEvent("popup");
  await page.keyboard.press("Enter");
  const note = await popup;
  await expect(note).toHaveURL(expected);
  expect(await note.evaluate(() => window.opener === null)).toBe(true);
  await note.close();
  await expect(page.getByRole("button", { name: "Expand neighbors" })).toBeEnabled();
});
