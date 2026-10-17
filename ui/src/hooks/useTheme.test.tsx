import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import bootstrap from "../../public/theme.js?raw";
import { useTheme } from "./useTheme";

beforeEach(() => {
  document.head.innerHTML = '<meta name="theme-color" content="#f4f6f8" />';
});

function systemTheme(dark: boolean) {
  const events = new EventTarget();
  vi.stubGlobal("matchMedia", vi.fn(() => ({
    matches: dark,
    addEventListener: events.addEventListener.bind(events),
    removeEventListener: events.removeEventListener.bind(events),
  })));
  return (matches: boolean) => events.dispatchEvent(Object.assign(new Event("change"), { matches }));
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("theme preference", () => {
  it.each([[true, "dark"], [false, "light"]] as const)("starts with the system preference (%s)", (dark, theme) => {
    systemTheme(dark);
    window.eval(bootstrap);
    const { result } = renderHook(useTheme);
    expect(result.current.theme).toBe(theme);
    expect(document.documentElement.dataset.theme).toBe(theme);
    expect(localStorage.length).toBe(0);
  });

  it("restores an explicit preference before React starts, overriding the system", () => {
    systemTheme(true);
    localStorage.setItem("brain-theme", "light");
    window.eval(bootstrap);
    expect(document.documentElement.dataset.theme).toBe("light");
    expect(document.documentElement.dataset.themePreference).toBe("light");
    expect(renderHook(useTheme).result.current.theme).toBe("light");
  });

  it("ignores invalid saved preferences", () => {
    systemTheme(true);
    localStorage.setItem("brain-theme", "invalid");
    window.eval(bootstrap);
    expect(renderHook(useTheme).result.current.theme).toBe("dark");
    expect(document.documentElement.dataset.themePreference).toBeUndefined();
  });

  it("follows system changes until the user chooses a theme and saves only that preference", () => {
    const changeSystem = systemTheme(false);
    window.eval(bootstrap);
    const { result } = renderHook(useTheme);
    act(() => { changeSystem(true); });
    expect(result.current.theme).toBe("dark");
    expect(localStorage.length).toBe(0);
    act(() => result.current.toggleTheme());
    expect(result.current.theme).toBe("light");
    expect(document.documentElement.dataset.theme).toBe("light");
    expect(Object.entries(localStorage)).toEqual([["brain-theme", "light"]]);
    act(() => { changeSystem(true); });
    expect(result.current.theme).toBe("light");
    act(() => result.current.toggleTheme());
    expect(result.current.theme).toBe("dark");
    expect(document.querySelector('meta[name="theme-color"]')).toHaveAttribute("content", "#0e1621");
  });

  it("removes the system listener on unmount", () => {
    const changeSystem = systemTheme(false);
    window.eval(bootstrap);
    const { unmount } = renderHook(useTheme);
    unmount();
    changeSystem(true);
    expect(document.documentElement.dataset.theme).toBe("light");
  });

  it("still starts when the browser blocks storage, and reports the limitation", () => {
    systemTheme(true);
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new DOMException("Storage disabled", "SecurityError");
    });
    const warning = vi.spyOn(console, "warn").mockImplementation(() => {});
    window.eval(bootstrap);
    expect(renderHook(useTheme).result.current.theme).toBe("dark");
    expect(warning).toHaveBeenCalledWith("Theme storage is unavailable; using the system color preference.");
  });

  it("still switches themes when persistence fails, and reports the limitation", () => {
    systemTheme(false);
    window.eval(bootstrap);
    const { result } = renderHook(useTheme);
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("Storage full", "QuotaExceededError");
    });
    const warning = vi.spyOn(console, "warn").mockImplementation(() => {});
    act(() => result.current.toggleTheme());
    expect(result.current.theme).toBe("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(warning).toHaveBeenCalledWith("Theme preference could not be saved; the selected theme will only apply to this page.");
  });
});
