import { expect, test } from "@playwright/test";
import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { createLayout } from "../src/features/pav/layouts";
import { buildVault, filterVault } from "../src/features/pav/vault";

function demoGraph() {
  const root = resolve(import.meta.dirname, "..", "demo-vault");
  return buildVault(Object.fromEntries(readdirSync(root, { recursive: true })
    .filter((name) => name.endsWith(".md"))
    .map((name) => [name.replaceAll("\\", "/"), readFileSync(resolve(root, name), "utf8")])));
}

function adminToken() {
  const line = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8")
    .split(/\r?\n/).find((value) => value.startsWith("BRAIN_UI_ADMIN_TOKEN="));
  if (!line) throw new Error("Run scripts\\Start-Development.ps1 before the PAV OS browser checks.");
  return line.slice("BRAIN_UI_ADMIN_TOKEN=".length);
}

test("PAV OS renders six file-backed Canvas layouts and supports inspection, filters, camera and responsive themes", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.name));
  page.on("console", (message) => {
    if (message.type() === "error" && /content security policy/i.test(message.text())) errors.push("CSP violation");
  });
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  await expect(page.locator(".graph-canvas canvas").first()).toBeVisible();
  const graphRequests: string[] = [];
  page.on("request", (request) => { if (new URL(request.url()).pathname.startsWith("/api/graph")) graphRequests.push(request.url()); });
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  await expect(page.getByRole("heading", { name: "PAV OS Agentic OS" })).toBeVisible();
  const canvas = page.locator(".pav-canvas-host canvas");
  const jump = page.getByRole("combobox", { name: "Jump to file", exact: true });
  await expect(canvas).toHaveAttribute("data-node-count", String(demoGraph().files.length));
  const workbench = (await page.locator(".pav-workbench").boundingBox())!;
  expect((await canvas.boundingBox())!.width).toBeGreaterThan(workbench.width - 4);
  await expect(page.getByText("IN YOUR ORBIT", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("complementary", { name: "Demo file details" })).toBeHidden();
  const hashes: string[] = [];
  for (const name of ["Ring", "Circle", "Areas", "Links", "Timeline", "3D Orbit"]) {
    await page.getByRole("button", { name, exact: true }).click();
    await expect(canvas).toHaveAttribute("data-view", name);
    hashes.push(await canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL()));
  }
  expect(new Set(hashes).size).toBe(6);
  await expect(page.getByRole("button", { name: "Play orbit" })).toBeDisabled();
  const beforeRotation = hashes[5];
  await page.getByRole("button", { name: "Rotate orbit right" }).click();
  expect(await canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL())).not.toBe(beforeRotation);
  await page.getByRole("button", { name: "Ring", exact: true }).click();
  await jump.selectOption("skills/research.md");
  const inspector = page.getByRole("complementary", { name: "Demo file details" });
  await expect(inspector.getByRole("heading", { name: "Research synthesis", exact: true })).toBeVisible();
  await expect(inspector.locator(".pav-file-icon")).toHaveCSS("color", "rgb(34, 183, 154)");
  await page.getByText("Read document", { exact: true }).click();
  await expect(inspector.locator("pre")).toContainText("Separate observations from interpretations");
  await inspector.getByRole("button", { name: /Atlas website/ }).click();
  await expect(jump).toHaveValue("business/atlas-project.md");
  const bounds = (await canvas.boundingBox())!;
  await canvas.click({ position: { x: bounds.width / 2, y: bounds.height / 2 } });
  await expect(jump).toHaveValue("AGENTS.md");
  await page.getByRole("button", { name: "Zoom in", exact: true }).click();
  const zoomed = await canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL());
  await page.getByRole("button", { name: "Fit vault" }).click();
  expect(await canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL())).not.toBe(zoomed);
  await page.getByLabel("Filter demo area").selectOption("business");
  await expect(canvas).toHaveAttribute("data-node-count", String(filterVault(demoGraph(), "", "business").files.length));
  await page.getByLabel("Search demo files").fill("no matching file");
  await expect(page.getByRole("status")).toContainText("No matching files");
  await expect(canvas).toHaveAttribute("data-node-count", "1");
  await jump.selectOption("skills/writing.md");
  await expect(page.getByLabel("Filter demo area")).toHaveValue("all");
  await expect(page.getByLabel("Search demo files")).toHaveValue("");
  await expect(canvas).toHaveAttribute("data-node-count", String(demoGraph().files.length));
  await jump.selectOption("AGENTS.md");
  await page.getByRole("button", { name: "Close file details" }).click();
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  await page.screenshot({ path: test.info().outputPath("pav-light.png"), fullPage: true });
  await page.getByRole("button", { name: /Switch to (light|dark) mode/ }).click();
  await page.screenshot({ path: test.info().outputPath("pav-desktop.png"), fullPage: true });
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByLabel("Search demo files")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await jump.focus();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(jump).not.toHaveValue("AGENTS.md");
  expect((await jump.boundingBox())!.width).toBeGreaterThan(100);
  expect(await page.locator(".pav-map").evaluate((element) => element.scrollLeft)).toBe(0);
  await expect(inspector.locator(".pav-file-icon")).toHaveCSS("color", "rgb(168, 116, 240)");
  await page.screenshot({ path: test.info().outputPath("pav-mobile.png"), fullPage: true });
  expect(graphRequests).toEqual([]);
  expect(errors).toEqual([]);
});

test("the graph fills the viewport in fullscreen and retains placement, inspection and dot-size settings", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  const map = page.getByRole("region", { name: "Demo vault exploration" });
  const canvas = page.locator(".pav-canvas-host canvas");
  const jump = page.getByRole("combobox", { name: "Jump to file", exact: true });
  await jump.selectOption("skills/research.md");
  await page.getByRole("button", { name: "Close file details" }).click();
  await canvas.focus();
  await page.keyboard.press("ArrowRight");
  await expect(canvas).toHaveAttribute("data-arranged-count", "1");
  await page.getByRole("button", { name: "Enter map fullscreen" }).click();
  await expect(page.getByRole("button", { name: "Exit map fullscreen" })).toHaveAttribute("aria-pressed", "true");
  expect(await map.evaluate((element) => document.fullscreenElement === element)).toBe(true);
  const bounds = (await map.boundingBox())!;
  expect(bounds.x).toBe(0);
  expect(bounds.y).toBe(0);
  expect(bounds.width).toBe(1440);
  expect(bounds.height).toBe(1000);
  expect((await canvas.boundingBox())!.height).toBeGreaterThan(600);
  await expect(canvas).toHaveAttribute("data-arranged-count", "1");
  await page.getByRole("slider", { name: "Dot size", exact: true }).focus();
  await page.keyboard.press("End");
  await expect(page.getByRole("slider", { name: "Dot size", exact: true })).toHaveValue("125");
  await page.getByRole("button", { name: "File details", exact: true }).click();
  await expect(page.getByRole("complementary", { name: "Demo file details" }).getByRole("heading", { name: "Research synthesis", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Close file details" }).click();
  await page.screenshot({ path: test.info().outputPath("pav-fullscreen.png") });
  await page.getByRole("button", { name: "Exit map fullscreen" }).click();
  await expect(page.getByRole("button", { name: "Enter map fullscreen" })).toHaveAttribute("aria-pressed", "false");
  await expect(canvas).toHaveAttribute("data-arranged-count", "1");
  await expect(jump).toHaveValue("skills/research.md");
  expect(await page.evaluate(() => document.fullscreenElement)).toBeNull();
});

test("PAV OS keeps real file exploration available without Canvas", async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(HTMLCanvasElement.prototype, "getContext", { value: () => null }));
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "Canvas rendering is unavailable" })).toBeVisible();
  await page.getByRole("combobox", { name: "Jump to file", exact: true }).selectOption("routines/weekly-review.md");
  await expect(page.getByRole("complementary", { name: "Demo file details" }).getByRole("heading", { name: "Weekly review", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Zoom in" })).toBeDisabled();
});

test("real node dragging stays on its named ring, works through pan and zoom, and resets free placements", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  const canvas = page.locator(".pav-canvas-host canvas");
  const jump = page.getByRole("combobox", { name: "Jump to file", exact: true });
  await expect(canvas).toHaveAttribute("data-node-count", String(demoGraph().files.length));
  await canvas.scrollIntoViewIfNeeded();
  let box = (await canvas.boundingBox())!;
  const graph = demoGraph();
  const layout = createLayout(graph, "Ring", box.width, box.height);
  const node = layout.nodes.find((item) => item.id === "skills/research.md")!;
  const cx = box.width / 2;
  const cy = box.height / 2;
  let angle = node.ring!.angle + 0.8;
  const ringPoint = () => ({ x: cx + Math.cos(angle) * node.ring!.rx, y: cy + Math.sin(angle) * node.ring!.ry });
  let point = ringPoint();
  await page.mouse.move(box.x + node.x, box.y + node.y);
  await page.mouse.down();
  await page.mouse.move(box.x + point.x, box.y + point.y, { steps: 10 });
  await page.mouse.up();
  await expect(jump).toHaveValue(node.id);
  await expect(canvas).toHaveAttribute("data-arranged-count", "1");
  await jump.selectOption("AGENTS.md");
  await canvas.click({ position: point });
  await expect(jump).toHaveValue(node.id);
  await page.getByRole("button", { name: "Close file details" }).click();

  await canvas.scrollIntoViewIfNeeded();
  box = (await canvas.boundingBox())!;
  await page.mouse.move(box.x + box.width - 80, box.y + box.height - 110);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width - 55, box.y + box.height - 90, { steps: 5 });
  await page.mouse.up();
  await page.getByRole("button", { name: "Zoom in", exact: true }).click();
  const screen = (position: { x: number; y: number }) => ({
    x: cx + (position.x - cx + 25) * 1.25, y: cy + (position.y - cy + 20) * 1.25,
  });
  await canvas.scrollIntoViewIfNeeded();
  box = (await canvas.boundingBox())!;
  const start = screen(point);
  angle += 0.5;
  point = ringPoint();
  const finish = screen(point);
  await page.mouse.move(box.x + start.x, box.y + start.y);
  await page.mouse.down();
  await page.mouse.move(box.x + finish.x, box.y + finish.y, { steps: 10 });
  await page.mouse.up();
  await jump.selectOption("AGENTS.md");
  await canvas.click({ position: finish });
  await expect(jump).toHaveValue(node.id);
  await page.getByRole("button", { name: "Reset node positions" }).click();
  await expect(canvas).toHaveAttribute("data-arranged-count", "0");

  await page.getByRole("button", { name: "Links", exact: true }).click();
  await canvas.scrollIntoViewIfNeeded();
  box = (await canvas.boundingBox())!;
  const free = createLayout(graph, "Links", box.width, box.height).nodes.find((item) => item.id === "business/atlas-project.md")!;
  const destination = { x: free.x + 65, y: free.y + 30 };
  await page.mouse.move(box.x + free.x, box.y + free.y);
  await page.mouse.down();
  await page.mouse.move(box.x + destination.x, box.y + destination.y, { steps: 10 });
  await page.mouse.up();
  await expect(canvas).toHaveAttribute("data-arranged-count", "1");
  await jump.selectOption("AGENTS.md");
  await canvas.click({ position: destination });
  await expect(jump).toHaveValue(free.id);
  await canvas.focus();
  await page.keyboard.press("ArrowRight");
  await jump.selectOption("AGENTS.md");
  await canvas.click({ position: { x: destination.x + 16, y: destination.y } });
  await expect(jump).toHaveValue(free.id);
  await page.getByRole("button", { name: "Reset node positions" }).click();
  await expect(canvas).toHaveAttribute("data-arranged-count", "0");
});

test("directed link flow and the entire orbit animate automatically and obey pause and reduced motion", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  const canvas = page.locator(".pav-canvas-host canvas");
  await expect(canvas).toHaveAttribute("data-motion", "running");
  await page.clock.install();
  await page.clock.pauseAt(new Date(Date.now() + 1000));
  const pixels = () => canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL());
  const first = await pixels();
  await page.clock.runFor(500);
  expect(await pixels()).not.toBe(first);
  await page.getByRole("button", { name: "Pause motion", exact: true }).click();
  const frozen = await pixels();
  await page.clock.runFor(500);
  expect(await pixels()).toBe(frozen);

  await page.getByRole("button", { name: "3D Orbit", exact: true }).click();
  await page.getByRole("checkbox", { name: "Links", exact: true }).uncheck();
  await page.getByRole("button", { name: "Resume motion", exact: true }).click();
  await expect(page.getByRole("button", { name: "Pause orbit", exact: true })).toBeEnabled();
  const orbit = await pixels();
  await page.clock.runFor(1000);
  expect(await pixels()).not.toBe(orbit);
  await page.getByRole("button", { name: "Pause orbit", exact: true }).click();
  await page.getByRole("button", { name: "Pause motion", exact: true }).click();
  const paused = await pixels();
  await page.clock.runFor(500);
  expect(await pixels()).toBe(paused);
  await page.getByRole("button", { name: "Resume motion", exact: true }).click();
  await page.getByRole("button", { name: "Play orbit", exact: true }).click();
  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect(canvas).toHaveAttribute("data-motion", "reduced");
  await expect(page.getByRole("button", { name: "Play orbit", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Resume motion", exact: true })).toBeDisabled();
  const reduced = await pixels();
  await page.clock.runFor(500);
  expect(await pixels()).toBe(reduced);
  await page.getByRole("button", { name: "Rotate orbit right" }).click();
  expect(await pixels()).not.toBe(reduced);
  await page.screenshot({ path: test.info().outputPath("pav-orbit.png"), fullPage: true });
});

test("real overview hubs frame dense Areas clusters, expose their files and keep area/type/membership independent", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.getByLabel("Admin token").fill(adminToken());
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  await page.getByRole("button", { name: "Areas", exact: true }).click();
  const canvas = page.locator(".pav-canvas-host canvas");
  const jump = page.getByRole("combobox", { name: "Jump to file", exact: true });
  const inspector = page.getByRole("complementary", { name: "Demo file details" });
  const graph = demoGraph();
  await expect(canvas).toHaveAttribute("data-area-hub-count", "6");
  await expect(canvas).toHaveAttribute("data-membership-count", "133");
  await expect(canvas).toHaveAttribute("data-link-count", "598");
  await canvas.evaluate((element) => { (element as HTMLCanvasElement).toDataURL(); });
  await page.screenshot({ path: test.info().outputPath("pav-area-clusters.png"), fullPage: true });
  const box = (await canvas.boundingBox())!;
  const layout = createLayout(graph, "Areas", box.width, box.height);
  const hub = layout.nodes.find((node) => node.id === "business/index.md")!;
  await canvas.click({ position: { x: hub.x, y: hub.y } });
  await expect(jump).toHaveValue(hub.id);
  await expect(canvas).toHaveAttribute("data-focused-area", "business");
  await expect(inspector).toContainText("25 real files in this area");
  const transform = await canvas.evaluate((element) => {
    const camera = (element as HTMLCanvasElement & { __zoom: { k: number; x: number; y: number } }).__zoom;
    return { k: camera.k, x: camera.x, y: camera.y };
  });
  expect(transform.k).toBeGreaterThan(1);
  const ownNotes = layout.nodes.filter((node) => node.file.area === "business" && node.file.kind === "note");
  for (const node of ownNotes) {
    expect(node.x * transform.k + transform.x).toBeGreaterThan(0);
    expect(node.x * transform.k + transform.x).toBeLessThan(box.width);
    expect(node.y * transform.k + transform.y).toBeGreaterThan(0);
    expect(node.y * transform.k + transform.y).toBeLessThan(box.height);
  }
  await page.getByRole("button", { name: "Close file details" }).click();
  const pixels = () => canvas.evaluate((element) => (element as HTMLCanvasElement).toDataURL());
  await pixels();
  await page.screenshot({ path: test.info().outputPath("pav-area-focus.png"), fullPage: true });
  await page.getByRole("checkbox", { name: "Links", exact: true }).uncheck();
  const withSpokes = await pixels();
  await page.getByRole("checkbox", { name: "Membership", exact: true }).uncheck();
  expect(await pixels()).not.toBe(withSpokes);
  await expect(canvas).toHaveAttribute("data-link-count", "598");
  await page.getByRole("checkbox", { name: "Membership", exact: true }).check();
  await page.getByRole("checkbox", { name: "Links", exact: true }).check();
  await jump.selectOption("content/index.md");
  const members = inspector.getByRole("region", { name: "Files in this area" });
  await expect(members.getByRole("button", { name: /Clear writing/ })).toBeVisible();
  await members.getByRole("button", { name: /Clear writing/ }).click();
  await expect(jump).toHaveValue("skills/writing.md");
  await expect(inspector).toContainText("Content / Skill / MARKDOWN");
  await expect(inspector.locator(".pav-file-icon")).toHaveCSS("color", "rgb(168, 116, 240)");
  await page.getByLabel("Filter demo area").selectOption("content");
  await page.getByLabel("Filter file type").selectOption("skill");
  await expect(canvas).toHaveAttribute("data-node-count", "3");
  await expect(canvas).toHaveAttribute("data-area-hub-count", "1");
  await jump.selectOption("personal/personal.md");
  await expect(page.getByLabel("Filter file type")).toHaveValue("all");
  await expect(page.getByLabel("Filter demo area")).toHaveValue("all");
  await expect(inspector.getByRole("heading", { name: "Personal", exact: true })).toBeVisible();
  await jump.selectOption("AGENTS.md");
  await page.getByRole("button", { name: "Close file details" }).click();
  await page.getByRole("button", { name: "Fit vault" }).click();
  await page.getByRole("button", { name: "Enter map fullscreen" }).click();
  await canvas.evaluate((element) => { (element as HTMLCanvasElement).toDataURL(); });
  await page.screenshot({ path: test.info().outputPath("pav-area-clusters-fullscreen.png") });
  await page.getByRole("button", { name: "Exit map fullscreen" }).click();
  await page.setViewportSize({ width: 375, height: 812 });
  await canvas.scrollIntoViewIfNeeded();
  await pixels();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath("pav-area-clusters-mobile.png"), fullPage: true });
});

test.describe("touch node dragging", () => {
  test.use({ hasTouch: true, viewport: { width: 375, height: 812 } });

  test("touch drags follow the portrait ring and keep tap inspection available", async ({ page, context }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto("/");
    await page.getByLabel("Admin token").fill(adminToken());
    await page.getByRole("button", { name: "Sign in to Brain" }).click();
    await page.getByRole("button", { name: "Open navigation" }).click();
    await page.getByRole("button", { name: "PAV OS", exact: true }).click();
    const canvas = page.locator(".pav-canvas-host canvas");
    const jump = page.getByRole("combobox", { name: "Jump to file", exact: true });
    await expect(canvas).toHaveAttribute("data-node-count", String(demoGraph().files.length));
    await canvas.scrollIntoViewIfNeeded();
    let box = (await canvas.boundingBox())!;
    const node = createLayout(demoGraph(), "Ring", box.width, box.height).nodes.find((item) => item.id === "skills/research.md")!;
    const destination = {
      x: box.width / 2 + Math.cos(node.ring!.angle + 0.65) * node.ring!.rx,
      y: box.height / 2 + Math.sin(node.ring!.angle + 0.65) * node.ring!.ry,
    };
    const session = await context.newCDPSession(page);
    await session.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x: box.x + node.x, y: box.y + node.y, id: 0 }] });
    for (let i = 1; i <= 8; i++) await session.send("Input.dispatchTouchEvent", {
      type: "touchMove",
      touchPoints: [{ x: box.x + node.x + (destination.x - node.x) * i / 8, y: box.y + node.y + (destination.y - node.y) * i / 8, id: 0 }],
    });
    await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    await expect(canvas).toHaveAttribute("data-arranged-count", "1");
    await expect(jump).toHaveValue(node.id);
    await jump.selectOption("AGENTS.md");
    await page.getByRole("button", { name: "Close file details" }).click();
    await canvas.scrollIntoViewIfNeeded();
    box = (await canvas.boundingBox())!;
    await page.touchscreen.tap(box.x + destination.x, box.y + destination.y);
    await expect(jump).toHaveValue(node.id);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await session.detach();
  });
});
