import { expect, test } from "@playwright/test";

test("theme follows the system, supports keyboard toggling, and persists without CSP errors", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  await page.emulateMedia({ colorScheme: "dark" });
  await page.goto("/");
  const root = page.locator("html");
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.locator(".signin-page")).toHaveCSS("background-color", "rgb(14, 22, 33)");
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute("content", "#0e1621");

  await page.emulateMedia({ colorScheme: "light" });
  await expect(root).toHaveAttribute("data-theme", "light");
  const toggle = page.getByRole("button", { name: "Switch to dark mode" });
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.locator('input[type="password"]')).toHaveCSS("background-color", "rgb(23, 34, 48)");
  await page.reload();
  await expect(root).toHaveAttribute("data-theme", "dark");
  await expect(page.getByLabel("Admin token")).toHaveValue("");
  expect(await page.evaluate(() => Object.entries(localStorage))).toEqual([["brain-theme", "dark"]]);
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);

  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByRole("button", { name: "Switch to light mode" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole("button", { name: "Switch to light mode" }).click();
  await expect(page.locator(".signin-page")).toHaveCSS("background-color", "rgb(255, 255, 255)");
  await page.reload();
  await expect(root).toHaveAttribute("data-theme", "light");
  expect(errors).toEqual([]);
});
