import { describe, expect, it, vi } from "vitest";
import { authenticate, Credentials } from "./client";

const credentials: Credentials = { token: "a-private-test-token" };

describe("Brain API client", () => {
  it("sends only the administrator token and lets the backend resolve identity", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: "codex", teamId: "platform", permissions: ["knowledge.read"] }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const profile = await authenticate(credentials);

    expect(profile.id).toBe("codex");
    expect(fetchMock).toHaveBeenCalledWith("/api/agents/me", {
      headers: {
        Accept: "application/json",
        Authorization: "Bearer a-private-test-token",
      },
      signal: undefined,
    });
  });

  it("reports authorization failures without exposing API response details", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ detail: "sensitive server diagnostic" }),
    }));

    await expect(authenticate(credentials)).rejects.toMatchObject({
      status: 401,
      message: "Brain API request failed (401).",
    });
  });
});
