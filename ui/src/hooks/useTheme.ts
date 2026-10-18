import { useState } from "react";

export type Theme = "light" | "dark";

function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0c1013" : "#f4f5f5");
}

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(
    () => document.documentElement.dataset.theme === "light" ? "light" : "dark",
  );

  function toggleTheme() {
    const next = theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.themePreference = next;
    applyTheme(next);
    setTheme(next);
    try {
      localStorage.setItem("brain-theme", next);
    } catch (error) {
      if (!(error instanceof DOMException) || !["SecurityError", "QuotaExceededError"].includes(error.name)) throw error;
      console.warn("Theme preference could not be saved; the selected theme will only apply to this page.");
    }
  }

  return { theme, toggleTheme };
}
