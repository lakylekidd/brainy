export const GROUPS = [
  { id: "business", label: "Business", color: "#529cff", description: "Independent work & projects" },
  { id: "personal", label: "Personal", color: "#badf61", description: "Life beyond the work" },
  { id: "youtube", label: "YouTube", color: "#ed79b3", description: "Stories worth sharing" },
  { id: "content", label: "Content", color: "#ac83ed", description: "Ideas into words" },
  { id: "career", label: "Career", color: "#49d5b4", description: "The longer journey" },
  { id: "skills", label: "Skills", color: "#c2ad67", description: "How to do the work" },
  { id: "memory", label: "Memory", color: "#92a5d6", description: "Context worth keeping" },
  { id: "routines", label: "Routines", color: "#60bcb5", description: "Small, repeatable rhythms" },
  { id: "applications", label: "Applications", color: "#b78fba", description: "Where things happen" },
] as const;

export type GroupId = typeof GROUPS[number]["id"] | "brain";
export interface VaultFile {
  id: string;
  title: string;
  group: GroupId;
  created: string;
  body: string;
}
export interface VaultLink { source: string; target: string }
export interface VaultGraph { files: VaultFile[]; links: VaultLink[] }
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
    const group = id === ROOT_FILE ? "brain" : GROUPS.find((item) => item.id === id.split("/")[0])?.id;
    if (!created || !parsedDate || !Number.isFinite(parsedDate.getTime()) || parsedDate.toISOString().slice(0, 10) !== created || !title || !group) {
      throw new DemoVaultError(`Invalid demo document: ${id}. Expected a known folder, heading and valid created date.`);
    }
    return { id, title, group, created, body };
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
  return { files, links };
}

export function loadDemoVault(): VaultGraph {
  const modules = import.meta.glob<string>("../../../demo-vault/**/*.md", {
    query: "?raw", import: "default", eager: true,
  });
  return buildVault(Object.fromEntries(Object.entries(modules).map(([path, content]) => [
    path.replace("../../../demo-vault/", ""), content,
  ])));
}

export function filterVault(graph: VaultGraph, query: string, group: string): VaultGraph {
  const term = query.trim().toLocaleLowerCase();
  const files = graph.files.filter((file) => file.id === ROOT_FILE
    || ((group === "all" || file.group === group) && `${file.title} ${file.id}`.toLocaleLowerCase().includes(term)));
  const ids = new Set(files.map((file) => file.id));
  return { files, links: graph.links.filter((link) => ids.has(link.source) && ids.has(link.target)) };
}

export function groupLabel(group: GroupId): string {
  return GROUPS.find((item) => item.id === group)?.label ?? "Brain";
}

export function fileColor(file: VaultFile): string {
  return GROUPS.find((group) => group.id === file.group)?.color ?? "#f3a45f";
}
