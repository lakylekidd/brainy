import { describe, expect, it } from "vitest";
import { buildVault, DemoVaultError, filterVault, GROUPS, loadDemoVault, ROOT_FILE } from "./vault";

const document = (body: string, created = "2026-10-01") => `---\ncreated: ${created}\n---\n${body}`;

describe("file-backed demo vault", () => {
  it("loads thirty actual Markdown files with valid dates, all nine folders and no synthetic nodes", () => {
    const graph = loadDemoVault();
    expect(graph.files).toHaveLength(30);
    expect(new Set(graph.files.map((file) => file.id)).size).toBe(30);
    expect(graph.files.find((file) => file.id === ROOT_FILE)?.group).toBe("brain");
    expect(new Set(graph.files.filter((file) => file.group !== "brain").map((file) => file.group)))
      .toEqual(new Set(GROUPS.map((group) => group.id)));
    expect(graph.links.length).toBeGreaterThan(70);
    for (const link of graph.links) {
      expect(graph.files.some((file) => file.id === link.source)).toBe(true);
      expect(graph.files.some((file) => file.id === link.target)).toBe(true);
    }
    expect(new Set(graph.links.map((link) => `${link.source}|${link.target}`)).size).toBe(graph.links.length);
    const visited = new Set([ROOT_FILE]);
    for (let i = 0; i < graph.files.length; i++) graph.links.forEach((link) => {
      if (visited.has(link.source)) visited.add(link.target);
    });
    expect(visited.size).toBe(graph.files.length);
  });

  it("resolves relative links and headings, ignores external links and deduplicates real directed references", () => {
    const graph = buildVault({
      [ROOT_FILE]: document("# Brain\n\n[Skill](skills/write.md)\n[Again](skills/write.md#steps)\n[External](https://example.test/x.md)"),
      "skills/write.md": document("# Write\n\n[Home](../AGENTS.md)\n[Self](write.md)"),
    });
    expect(graph.links).toEqual([
      { source: ROOT_FILE, target: "skills/write.md" }, { source: "skills/write.md", target: ROOT_FILE },
    ]);
    expect(graph.files[0].body).not.toContain("created:");
  });

  it.each([
    [{ "skills/write.md": document("# Write") }, "missing AGENTS.md"],
    [{ [ROOT_FILE]: document("# Brain\n\n[Broken](skills/missing.md)") }, "Broken demo link"],
    [{ [ROOT_FILE]: document("# Brain\n\n[Escape](../outside.md)") }, "leaves the demo vault"],
    [{ [ROOT_FILE]: document("# Brain", "2026-02-30") }, "Invalid demo document"],
    [{ [ROOT_FILE]: "# Missing date" }, "Invalid demo document"],
    [{ [ROOT_FILE]: document("No heading") }, "Invalid demo document"],
    [{ [ROOT_FILE]: document("# Brain"), "other/file.md": document("# Other") }, "Invalid demo document"],
  ])("rejects invalid fixture data explicitly", (sources, message) => {
    expect(() => buildVault(sources)).toThrow(DemoVaultError);
    expect(() => buildVault(sources)).toThrow(message);
  });

  it("filters by title, path and group while preserving the entry point and loaded endpoints", () => {
    const graph = loadDemoVault();
    const filtered = filterVault(graph, "  RESEARCH  ", "skills");
    expect(filtered.files.map((file) => file.id)).toEqual([ROOT_FILE, "skills/research.md"]);
    expect(filtered.links).toEqual([{ source: ROOT_FILE, target: "skills/research.md" }]);
    expect(filterVault(graph, "youtube/", "all").files).toHaveLength(4);
    expect(filterVault(graph, "no such file", "all")).toEqual({
      files: [graph.files.find((file) => file.id === ROOT_FILE)], links: [],
    });
    expect(filterVault(graph, "", "routines").files).toHaveLength(5);
  });
});
