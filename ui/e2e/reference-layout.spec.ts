import { expect, test } from "@playwright/test";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

test("the whole Brain workspace uses the reference palette, fonts and hairline layout without losing its routes", async ({ page, request }) => {
  const line = readFileSync(resolve(import.meta.dirname, "..", "..", ".env"), "utf8")
    .split(/\r?\n/).find((value) => value.startsWith("BRAIN_UI_ADMIN_TOKEN="));
  if (!line) throw new Error("Initialize the development stack before the workspace browser checks.");
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" && /content security policy/i.test(message.text())) errors.push("CSP violation");
  });
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.emulateMedia({ colorScheme: "light", reducedMotion: "reduce" });
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByLabel("Admin token").fill(line.slice("BRAIN_UI_ADMIN_TOKEN=".length));
  await page.getByRole("button", { name: "Sign in to Brain" }).click();
  await expect(page.locator(".graph-canvas canvas").first()).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  await expect(page.locator(".sidebar")).toHaveCount(0);
  await expect(page.locator("body")).toHaveCSS("background-color", "rgb(12, 16, 19)");
  await expect(page.locator("body")).toHaveCSS("font-family", '"JetBrains Mono", ui-monospace, Menlo, monospace');
  await expect(page.locator(".metric-value").last()).toHaveCSS("font-family", '"Barlow Condensed", Impact, "Arial Narrow", sans-serif');
  const tokens = await page.locator("html").evaluate((element) => {
    const css = getComputedStyle(element);
    return ["--h", "--ink2", "--mute", "--raise", "--line", "--line2"].map((name) => css.getPropertyValue(name).trim());
  });
  expect(tokens).toEqual(["#f07a2e", "#b0bec8", "#81919b", "#18232a", "rgba(255, 255, 255, .07)", "rgba(255, 255, 255, .15)"]);
  const provenance = JSON.parse(readFileSync(resolve(import.meta.dirname, "..", "public", "fonts", "PROVENANCE.json"), "utf8"));
  for (const font of ["JetBrainsMono.ttf", "BarlowCondensed-Medium.ttf", "BarlowCondensed-SemiBold.ttf"]) {
    const response = await request.get(`/fonts/${font}`);
    expect(response.status()).toBe(200);
    expect(createHash("sha256").update(await response.body()).digest("hex")).toBe(provenance[font].sha256);
  }
  expect(await page.evaluate(() => document.fonts.check('12px "JetBrains Mono"') && document.fonts.check('500 24px "Barlow Condensed"'))).toBe(true);
  const grid = (await page.locator(".dashboard-workspace").boundingBox())!;
  const left = (await page.getByRole("complementary", { name: "Brain overview" }).boundingBox())!;
  const centre = (await page.locator(".dashboard-workspace .workspace-centre").boundingBox())!;
  const right = (await page.getByRole("complementary", { name: "Review and activity" }).boundingBox())!;
  expect(left.width / grid.width).toBeCloseTo(0.2, 2);
  expect(centre.width / grid.width).toBeCloseTo(0.6, 2);
  expect(right.width / grid.width).toBeCloseTo(0.2, 2);
  expect(centre.x).toBeCloseTo(left.x + left.width, 0);
  await page.screenshot({ path: test.info().outputPath("reference-brain-dashboard.png"), fullPage: true });
  for (const [route, heading] of [
    ["PAV OS", "PAV OS Agentic OS"], ["Knowledge graph", "Knowledge graph"],
    ["Agents", "Agents"], ["Permissions", "Roles & permissions"], ["Settings", "Settings"],
  ]) {
    await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: route, exact: true }).click();
    await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
    await expect(page.locator("body")).toHaveCSS("background-color", "rgb(12, 16, 19)");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.getByRole("button", { name: "Agents", exact: true }).click();
  await expect(page.getByRole("region", { name: "Agent directory" })).toBeVisible();
  await page.screenshot({ path: test.info().outputPath("reference-brain-agents.png"), fullPage: true });
  await page.getByRole("button", { name: "PAV OS", exact: true }).click();
  await expect(page.locator(".pav-canvas-host canvas")).toHaveAttribute("data-node-count", "134");
  await expect.poll(() => page.locator(".pav-canvas-host canvas").evaluate((element) => {
    const canvas = element as HTMLCanvasElement;
    const dpr = Math.min(devicePixelRatio, 2);
    return Math.abs(canvas.width - canvas.getBoundingClientRect().width * dpr);
  })).toBeLessThanOrEqual(1);
  // Flush Chromium's Canvas texture before capturing the full-page compositor.
  await page.locator(".pav-canvas-host canvas").evaluate((canvas) => { (canvas as HTMLCanvasElement).toDataURL(); });
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await page.screenshot({ path: test.info().outputPath("reference-brain-pav.png"), fullPage: true });
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  await expect(page.locator("body")).toHaveCSS("background-color", "rgb(244, 245, 245)");
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByRole("navigation", { name: "Main navigation" })).toBeHidden();
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(page.getByRole("button", { name: "PAV OS", exact: true })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("button", { name: "Open navigation" })).toBeFocused();
  await page.getByRole("button", { name: "Open navigation" }).click();
  await page.getByRole("button", { name: "Dashboard", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Good to see you." })).toBeVisible();
  await expect(page.locator(".graph-canvas canvas").first()).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Main navigation" })).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath("reference-brain-mobile.png"), fullPage: true });
  expect(errors).toEqual([]);
});
