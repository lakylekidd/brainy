import { expect, test } from "@playwright/test";

test("reference dark is the default, with persistent keyboard-accessible light mode and no CSP errors", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  await page.emulateMedia({ colorScheme: "light" });
  await page.goto("/");
  const root = page.locator("html");
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.locator(".signin-page")).toHaveCSS("background-color", "rgb(12, 16, 19)");
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute("content", "#0c1013");
  expect(await page.evaluate(() => localStorage.length)).toBe(0);

  await page.emulateMedia({ colorScheme: "dark" });
  await page.emulateMedia({ colorScheme: "light" });
  await expect(root).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  const toggle = page.getByRole("button", { name: "Switch to dark mode" });
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.locator('input[type="password"]')).toHaveCSS("background-color", "rgb(17, 24, 29)");
  await page.reload();
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.getByLabel("Admin token")).toHaveValue("");
  expect(await page.evaluate(() => Object.entries(localStorage))).toEqual([["brain-theme", "dark"]]);
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);

  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByRole("button", { name: "Switch to light mode" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  await expect(page.locator(".signin-page")).toHaveCSS("background-color", "rgb(244, 245, 245)");
  await page.reload();
  await expect(root).toHaveAttribute("data-theme", "light");
  expect(errors).toEqual([]);
});
