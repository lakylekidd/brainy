export const AREAS = [
  { id: "business", label: "Business", color: "#4a8cf0", description: "Independent work & projects" },
  { id: "personal", label: "Personal", color: "#8cc421", description: "Life beyond the work" },
  { id: "youtube", label: "YouTube", color: "#e85e8e", description: "Stories worth sharing" },
  { id: "content", label: "Content", color: "#a874f0", description: "Ideas into words" },
  { id: "career", label: "Career", color: "#22b79a", description: "The longer journey" },
  { id: "system", label: "System", color: "#92999f", description: "Shared capabilities & workspace context" },
] as const;

export type AreaId = typeof AREAS[number]["id"];
export const FILE_KINDS = ["area", "note", "skill", "memory", "routine", "application"] as const;
export type FileKind = typeof FILE_KINDS[number] | "root";
export const KIND_LABELS: Record<FileKind, string> = {
  root: "Entry point", area: "Area overview", note: "Note", skill: "Skill",
  memory: "Memory", routine: "Routine", application: "Application",
};
export interface VaultFile {
  id: string;
  title: string;
  area: AreaId | null;
  kind: FileKind;
  layer: "core" | "knowledge" | "skills" | "memory" | "routines" | "applications";
  created: string;
  body: string;
}
export interface VaultLink { source: string; target: string }
export interface VaultGraph { files: VaultFile[]; links: VaultLink[]; memberships: VaultLink[] }
export const ROOT_FILE = "AGENTS.md";
export class DemoVaultError extends Error {}

function resolveLink(source: string, target: string): string {
  const parts = source.split("/").slice(0, -1);
  for (const part of target.split("#")[0].split("/")) {
    if (part === "..") {
      if (!parts.length) throw new DemoVaultError(`Link leaves the demo vault: ${source} -> ${target}`);
      parts.pop();
    } else if (part !== ".") {
      parts.push(part);
    }
  }
  return parts.join("/");
}

export function buildVault(sources: Record<string, string>): VaultGraph {
  const files = Object.entries(sources).sort(([a], [b]) => a.localeCompare(b)).map(([id, markdown]): VaultFile => {
    const frontmatter = /^---\r?\n([\s\S]*?)\r?\n---\r?\n/.exec(markdown);
    const created = frontmatter && /^created: (\d{4}-\d{2}-\d{2})\r?$/m.exec(frontmatter[1])?.[1];
    const parsedDate = created ? new Date(`${created}T00:00:00Z`) : null;
    const body = markdown.slice(frontmatter?.[0].length ?? 0).trim();
    const title = /^# (.+)\r?$/m.exec(body)?.[1].trim();
    const folder = id.split("/")[0];
    const defaults: Record<string, FileKind> = { skills: "skill", memory: "memory", routines: "routine", applications: "application" };
    const fields = frontmatter?.[1] ?? "";
    const areaField = /^area:\s*(.*?)\r?$/m.exec(fields)?.[1];
    const kindField = /^kind:\s*(.*?)\r?$/m.exec(fields)?.[1];
    const area = id === ROOT_FILE ? null : AREAS.find((item) => item.id === (areaField ?? (AREAS.some((item) => item.id === folder) ? folder : "system")))?.id;
    const kind: FileKind | undefined = id === ROOT_FILE ? "root" : FILE_KINDS.find((item) => item === (kindField ?? defaults[folder] ?? "note"));
    if (!created || !parsedDate || !Number.isFinite(parsedDate.getTime()) || parsedDate.toISOString().slice(0, 10) !== created || !title
      || !kind || (id !== ROOT_FILE && (!area
        || (!defaults[folder] && !AREAS.some((item) => item.id === folder))))
      || (id === ROOT_FILE && (areaField !== undefined || (kindField !== undefined && kindField !== "root")))) {
      throw new DemoVaultError(`Invalid demo document: ${id}. Expected a known folder, heading, valid created date and supported area/kind metadata.`);
    }
    const layers: Record<FileKind, VaultFile["layer"]> = {
      root: "core", area: "core", note: "knowledge", skill: "skills", memory: "memory", routine: "routines", application: "applications",
    };
    return { id, title, area: area ?? null, kind, layer: layers[kind], created, body };
  });
  const ids = new Set(files.map((file) => file.id));
  if (!ids.has(ROOT_FILE)) throw new DemoVaultError("The demo vault is missing AGENTS.md.");
  const links: VaultLink[] = [];
  for (const file of files) {
    const targets = new Set<string>();
    for (const match of file.body.matchAll(/\[[^\]]*\]\(([^)\s]+\.md(?:#[^)\s]*)?)\)/g)) {
      if (/^(?:[a-z]+:|\/)/i.test(match[1])) continue;
      const target = resolveLink(file.id, match[1]);
      if (!ids.has(target)) throw new DemoVaultError(`Broken demo link: ${file.id} -> ${target}`);
      if (target !== file.id) targets.add(target);
    }
    targets.forEach((target) => links.push({ source: file.id, target }));
  }
  const hubs = new Map<AreaId, VaultFile>();
  for (const file of files.filter((item) => item.kind === "area")) {
    if (!file.area || hubs.has(file.area)) throw new DemoVaultError(`Duplicate demo area overview: ${file.area}.`);
    hubs.set(file.area, file);
  }
  const memberships = files.flatMap((file): VaultLink[] => {
    const hub = file.area && hubs.get(file.area);
    return hub ? [{ source: file.kind === "area" ? ROOT_FILE : hub.id, target: file.id }] : [];
  });
  return { files, links, memberships };
}

export function loadDemoVault(): VaultGraph {
  const modules = import.meta.glob<string>("../../../demo-vault/**/*.md", {
    query: "?raw", import: "default", eager: true,
  });
  return buildVault(Object.fromEntries(Object.entries(modules).map(([path, content]) => [
    path.replace("../../../demo-vault/", ""), content,
  ])));
}

export function filterVault(graph: VaultGraph, query: string, area: string, kind = "all"): VaultGraph {
  const term = query.trim().toLocaleLowerCase();
  const matches = graph.files.filter((file) => file.id !== ROOT_FILE
    && (area === "all" || file.area === area) && (kind === "all" || file.kind === kind)
    && `${file.title} ${file.id}`.toLocaleLowerCase().includes(term));
  const areas = new Set(matches.map((file) => file.area));
  const matchedIds = new Set(matches.map((file) => file.id));
  const files = graph.files.filter((file) => file.id === ROOT_FILE || matchedIds.has(file.id)
    || (file.kind === "area" && areas.has(file.area)));
  const ids = new Set(files.map((file) => file.id));
  return { files, links: graph.links.filter((link) => ids.has(link.source) && ids.has(link.target)),
    memberships: graph.memberships.filter((link) => ids.has(link.source) && ids.has(link.target)) };
}

export function areaLabel(area: AreaId | null): string {
  return AREAS.find((item) => item.id === area)?.label ?? "Brain";
}

export function fileColor(file: VaultFile): string {
  return AREAS.find((area) => area.id === file.area)?.color ?? "#f07a2e";
}
