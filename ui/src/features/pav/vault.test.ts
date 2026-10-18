import { describe, expect, it } from "vitest";
import { AREAS, buildVault, DemoVaultError, fileColor, filterVault, loadDemoVault, ROOT_FILE } from "./vault";

const document = (body: string, created = "2026-10-01") => `---\ncreated: ${created}\n---\n${body}`;

describe("file-backed demo vault", () => {
  it("loads 134 real Markdown files with six real overview hubs and 20-30 files per interest area", () => {
    const graph = loadDemoVault();
    expect(graph.files).toHaveLength(134);
    expect(new Set(graph.files.map((file) => file.id)).size).toBe(134);
    expect(graph.files.find((file) => file.id === ROOT_FILE)).toMatchObject({ kind: "root", area: null, layer: "core" });
    expect(graph.files.filter((file) => file.kind === "area")).toHaveLength(6);
    expect(new Set(graph.files.filter((file) => file.area).map((file) => file.area)))
      .toEqual(new Set(AREAS.map((area) => area.id)));
    for (const area of AREAS.filter((area) => area.id !== "system")) {
      const members = graph.files.filter((file) => file.area === area.id);
      expect(members.length).toBeGreaterThanOrEqual(20);
      expect(members.length).toBeLessThanOrEqual(30);
      expect(members.some((file) => file.kind === "skill")).toBe(true);
      expect(members.some((file) => file.kind === "routine")).toBe(true);
    }
    expect(graph.links.length).toBeGreaterThan(450);
    expect(graph.memberships).toHaveLength(graph.files.length - 1);
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

  it("separates area, kind and layer independently of folder depth and preserves area colors on skills", () => {
    const graph = loadDemoVault();
    const writing = graph.files.find((file) => file.id === "skills/writing.md")!;
    expect(writing).toMatchObject({ area: "content", kind: "skill", layer: "skills" });
    expect(fileColor(writing)).toBe("#a874f0");
    expect(graph.files.find((file) => file.id === "business/projects/atlas/discovery.md")).toMatchObject({ area: "business", kind: "note", layer: "knowledge" });
    expect(graph.files.find((file) => file.id === "personal/personal.md")?.body).toContain("[compass](compass.md)");
    expect(graph.memberships).toContainEqual({ source: "content/index.md", target: writing.id });
    expect(graph.links).not.toContainEqual({ source: "content/index.md", target: "applications/publisher.md" });
    expect(graph.memberships).toContainEqual({ source: "content/index.md", target: "applications/publisher.md" });
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
    [{ [ROOT_FILE]: document("# Brain"), "skills/write.md": "---\ncreated: 2026-10-01\narea: unknown\n---\n# Write" }, "Invalid demo document"],
    [{ [ROOT_FILE]: document("# Brain"), "skills/write.md": "---\ncreated: 2026-10-01\nkind: run\n---\n# Write" }, "Invalid demo document"],
    [{ [ROOT_FILE]: document("# Brain"), "business/index.md": "---\ncreated: 2026-10-01\nkind: area\n---\n# Business", "business/other.md": "---\ncreated: 2026-10-01\nkind: area\n---\n# Other" }, "Duplicate demo area"],
  ])("rejects invalid fixture data explicitly", (sources, message) => {
    expect(() => buildVault(sources)).toThrow(DemoVaultError);
    expect(() => buildVault(sources)).toThrow(message);
  });

  it("filters area and type independently and retains only the root and matching area overview context", () => {
    const graph = loadDemoVault();
    const filtered = filterVault(graph, "  RESEARCH  ", "career", "skill");
    expect(filtered.files.map((file) => file.id)).toEqual([ROOT_FILE, "career/index.md", "skills/research.md"]);
    expect(filtered.links).toContainEqual({ source: ROOT_FILE, target: "skills/research.md" });
    expect(filtered.memberships).toContainEqual({ source: "career/index.md", target: "skills/research.md" });
    expect(filterVault(graph, "no such file", "all")).toEqual({
      files: [graph.files.find((file) => file.id === ROOT_FILE)], links: [], memberships: [],
    });
    const routines = filterVault(graph, "", "all", "routine");
    expect(routines.files.filter((file) => file.kind === "routine")).toHaveLength(6);
    expect(routines.files.every((file) => ["root", "area", "routine"].includes(file.kind))).toBe(true);
    for (const link of [...routines.links, ...routines.memberships]) {
      expect(routines.files.some((file) => file.id === link.source)).toBe(true);
      expect(routines.files.some((file) => file.id === link.target)).toBe(true);
    }
  });
});
