import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  workers: 1,
  use: {
    baseURL: process.env.BRAIN_UI_URL ?? "http://127.0.0.1:5173",
    browserName: "chromium",
    trace: "off",
  },
});
