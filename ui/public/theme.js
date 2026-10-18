// Apply the theme before styles load, without requiring an inline script in the CSP.
(() => {
  let preference = null;
  try {
    const saved = localStorage.getItem("brain-theme");
    if (saved === "light" || saved === "dark") preference = saved;
  } catch (error) {
    if (!(error instanceof DOMException) || error.name !== "SecurityError") throw error;
    console.warn("Theme storage is unavailable; using the reference dark theme.");
  }
  const theme = preference ?? "dark";
  document.documentElement.dataset.theme = theme;
  if (preference) document.documentElement.dataset.themePreference = preference;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0c1013" : "#f4f5f5");
})();
