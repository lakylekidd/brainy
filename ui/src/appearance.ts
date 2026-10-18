import type { Theme } from "./hooks/useTheme";

export const canvasFont = "'JetBrains Mono', ui-monospace, Menlo, monospace";
export function canvasPalette(theme: Theme) {
  return theme === "dark"
    ? { ink: "#e2e6e9", surface: "#0c1013", muted: "#81919b", line: "#ffffff26" }
    : { ink: "#202b32", surface: "#f4f5f5", muted: "#607580", line: "#16252e3d" };
}
