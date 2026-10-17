// Apply the theme before styles load, without requiring an inline script in the CSP.
(() => {
  let preference = null;
  try {
    const saved = localStorage.getItem("brain-theme");
    if (saved === "light" || saved === "dark") preference = saved;
  } catch (error) {
    if (!(error instanceof DOMException) || error.name !== "SecurityError") throw error;
    console.warn("Theme storage is unavailable; using the system color preference.");
  }
  const theme = preference ?? (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  document.documentElement.dataset.theme = theme;
  if (preference) document.documentElement.dataset.themePreference = preference;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0e1621" : "#f4f6f8");
})();
