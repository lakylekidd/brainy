import { describe, expect, it } from "vitest";
import { webObsidianDocumentLink } from "./webObsidian";

describe("WebObsidian document URLs", () => {
  it("uses the confirmed note route and encodes each canonical path segment", () => {
    const result = webObsidianDocumentLink("https://obsidian.example", "\u{1f4c2}Projects/Homelab Brain/ui-prompt.md");
    expect(result.href).toBe("https://obsidian.example/note/%F0%9F%93%82Projects/Homelab%20Brain/ui-prompt.md");
    expect(webObsidianDocumentLink("https://obsidian.example/", "root.md").href)
      .toBe("https://obsidian.example/note/root.md");
    expect(webObsidianDocumentLink("https://obsidian.example", "Root.MD").href)
      .toBe("https://obsidian.example/note/Root.MD");
  });

  it("preserves an operator-controlled deployment prefix and port", () => {
    expect(webObsidianDocumentLink("http://localhost:3000/obsidian/", "folder/note.md").href)
      .toBe("http://localhost:3000/obsidian/note/folder/note.md");
  });

  it.each([undefined, ""])("reports missing configuration (%s)", (base) => {
    expect(webObsidianDocumentLink(base, "root.md")).toEqual({ error: expect.stringContaining("not configured") });
  });

  it.each([
    "not a URL", "/obsidian", "//evil.example", "javascript:alert(1)", "data:text/html,test",
    "ftp://obsidian.example", "https://user:password@obsidian.example", "https://obsidian.example?token=private",
    "https://obsidian.example/#route", "https://obsidian.example?", "https://obsidian.example#",
    " https://obsidian.example", "https://obsidian.example\n", "https:\\\\evil.example",
    "https://obsidian.example\\@evil.example",
  ])("rejects unsafe or ambiguous base configuration (%s)", (base) => {
    const result = webObsidianDocumentLink(base, "root.md");
    expect(result.href).toBeUndefined();
    expect(result.error).toContain("WebObsidianBaseUrl is invalid");
    expect(result.error).not.toContain(base);
  });

  it.each([
    "", "/root.md", "//evil.example/root.md", "../root.md", "folder/../root.md", "./root.md",
    "folder//root.md", "folder\\root.md", "C:/root.md", "https://evil.example/root.md",
    "root.txt", "folder/\u0000root.md", "folder/\ud800.md",
  ])("rejects noncanonical paths (%s)", (path) => {
    expect(webObsidianDocumentLink("https://obsidian.example", path))
      .toEqual({ error: expect.stringContaining("invalid canonical path") });
  });

  it.each([
    "folder/a #?&%.md", "folder/%2e%2e/%2f%2fevil.example.md", "folder/@evil.example.md",
  ])("cannot change origin, route or query through document path characters (%s)", (path) => {
    const result = webObsidianDocumentLink("https://obsidian.example/prefix", path);
    const url = new URL(result.href!);
    expect(url.origin).toBe("https://obsidian.example");
    expect(url.pathname).toBe(`/prefix/note/${path.split("/").map(encodeURIComponent).join("/")}`);
    expect(url.search).toBe("");
    expect(url.hash).toBe("");
    expect(url.pathname.slice("/prefix/note/".length).split("/").map(decodeURIComponent).join("/")).toBe(path);
  });
});
