import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import Today from "./Today";

afterEach(() => { cleanup(); vi.useRealTimers(); });
describe("local clock widget", () => {
  it("renders actual local time and date and disposes its timer", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-10-18T10:20:30Z"));
    const { unmount } = render(<Today />);
    const time = screen.getByRole("region", { name: "Local date and time" }).querySelector("time")!;
    expect(time).toHaveAttribute("datetime", "2026-10-18T10:20:30.000Z");
    expect(time).toHaveTextContent(":30");
    act(() => { vi.advanceTimersByTime(1000); });
    expect(time).toHaveAttribute("datetime", "2026-10-18T10:20:31.000Z");
    expect(vi.getTimerCount()).toBe(1);
    unmount();
    expect(vi.getTimerCount()).toBe(0);
  });
});
