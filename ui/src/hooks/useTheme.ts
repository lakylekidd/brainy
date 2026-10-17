import { useEffect, useRef, useState } from "react";

export type Theme = "light" | "dark";

function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0e1621" : "#f4f6f8");
}

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(
    () => document.documentElement.dataset.theme === "dark" ? "dark" : "light",
  );
  const preference = useRef(document.documentElement.dataset.themePreference);

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    function updateSystemTheme(event: MediaQueryListEvent) {
      if (preference.current === "light" || preference.current === "dark") return;
      const next = event.matches ? "dark" : "light";
      applyTheme(next);
      setTheme(next);
    }
    media.addEventListener("change", updateSystemTheme);
    return () => media.removeEventListener("change", updateSystemTheme);
  }, []);

  function toggleTheme() {
    const next = theme === "dark" ? "light" : "dark";
    preference.current = next;
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
