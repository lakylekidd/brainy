export type DocumentLink = { href: string; error?: never } | { href?: never; error: string };

export function webObsidianDocumentLink(baseUrl: string | undefined, path: string): DocumentLink {
  if (!baseUrl)
    return { error: "WebObsidian is not configured. Ask the operator to set WebObsidianBaseUrl." };

  let base: URL;
  try {
    base = new URL(baseUrl);
  } catch (error) {
    if (!(error instanceof TypeError)) throw error;
    return { error: "WebObsidianBaseUrl is invalid. Configure an absolute HTTP(S) URL without credentials, a query or a fragment." };
  }
  if (!/^https?:\/\//i.test(baseUrl) || baseUrl !== baseUrl.trim() ||
      /[\u0000-\u0020\u007f\\?#]/.test(baseUrl) ||
      !["https:", "http:"].includes(base.protocol) || base.username || base.password) {
    return { error: "WebObsidianBaseUrl is invalid. Configure an absolute HTTP(S) URL without credentials, a query or a fragment." };
  }

  const segments = path.split("/");
  if (!/\.md$/i.test(path) || /[\u0000-\u001f\u007f-\u009f\\:]/.test(path) ||
      segments.some((segment) => !segment || segment === "." || segment === "..")) {
    return { error: "This document has an invalid canonical path. No WebObsidian link is available." };
  }
  try {
    const encodedPath = segments.map((segment) => encodeURIComponent(segment)).join("/");
    return { href: `${base.origin}${base.pathname.replace(/\/+$/, "")}/note/${encodedPath}` };
  } catch (error) {
    if (!(error instanceof URIError)) throw error;
    return { error: "This document has an invalid canonical path. No WebObsidian link is available." };
  }
}
