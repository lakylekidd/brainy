import { Moon, Sun } from "lucide-react";
import { Theme } from "../hooks/useTheme";

export default function ThemeToggle({ theme, onToggle }: { theme: Theme; onToggle: () => void }) {
  const label = `Switch to ${theme === "dark" ? "light" : "dark"} mode`;
  return (
    <button
      className="icon-button theme-toggle"
      type="button"
      aria-label={label}
      title={label}
      onClick={onToggle}
    >
      {theme === "dark" ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
    </button>
  );
}
