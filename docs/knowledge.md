# Knowledge — Milestone 2

## Boundaries

`KnowledgeDocument` describes an indexed file; it stores no authoritative full Markdown body. Application owns vault/index contracts, Markdown chunking and read/search/indexing services. Infrastructure implements the read-only vault interface and PostgreSQL persistence. [Milestone 3](knowledge-writes.md) adds a separate reviewed writer; this module does not write files. [Milestone 5 MCP tools](mcp.md) reuse these services. [Milestone 6](metadata-enrichment.md) scans frontmatter and queues reviewed metadata patches through the same writer.

The canonical vault remains the filesystem/Git repository. Database chunk text and vectors are rebuildable search caches. The scanner never edits Markdown; the separate writer can apply approved changes and create local commits. Remote Git synchronization remains deferred.

## Configuration and operation

Set `Knowledge:VaultPath` to the vault's absolute directory (`Knowledge__VaultPath` in environment variables). API and Worker must see the same vault and use the same embedding configuration. Compose now mounts the dedicated `.local/vault` Git checkout read-only in the API and read-write in the worker. Development agents receive `knowledge.read` and `knowledge.propose`; administrators additionally receive `knowledge.admin`. Production operators must explicitly provision permission rows and grants.

The worker scans at startup and every `Worker:KnowledgeScanIntervalSeconds` (default 60; Compose 30). A PostgreSQL session advisory lease coordinates worker and administrative scans across processes. Concurrent scans return `alreadyRunning: true`. This coordinates index maintenance only; it does not lock canonical files. API reads and searches remain available while a scan runs.

Each scan enumerates `.md` files, hashes raw UTF-8 bytes and compares content hash, embedding provider/model/dimensions, and chunker version. Matching documents are skipped without generating embeddings. Modified documents keep their database ID; replacement chunks, document metadata and audit are committed together. Removed files and their chunks are pruned after a complete directory enumeration. File renames create a new document identity and remove the old one.

The scanner reads a file again after embedding generation. If it changed, that file's update is deferred to the next scan. Provider/database failures abort the scan with already committed documents intact. Per-file missing/invalid/oversized content is reported in `failures`, preserving any previous indexed version; search independently rejects stale hits. If the vault is unavailable or directory enumeration fails, pruning is never attempted. A truly empty mounted vault is authoritative and clears old rows; deployers must distinguish an empty directory from a missing mount.

UI Milestone 2 also indexes an optional standalone top-level `graph_references`
frontmatter list (up to 100 explicit stable graph IDs). It does not infer links
from body text or copy arbitrary frontmatter into responses. Invalid reference
metadata is a per-file scan failure; unrelated malformed YAML without this
field remains indexable as before. The `KnowledgeGraph` migration adds the
rebuildable reference array, parser version and reverse-lookup GIN index.
Existing files are refreshed once after migration even if their content/model
has not changed. See [graph references and source behavior](ui.md#sources-relationships-and-synchronization).

## Read and search

Using the `$headers` and `$baseUrl` setup from the README:

```powershell
$hits = Invoke-RestMethod "$baseUrl/api/knowledge/search" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    query = 'Proxmox corosync cluster recovery'
    pathPrefix = 'Infrastructure'
    limit = 5
} | ConvertTo-Json)

$document = Invoke-RestMethod "$baseUrl/api/knowledge/document?path=Infrastructure/Proxmox/nacht-02.md" -Headers $headers
$document.revisionToken
```

Alternatively, read a search hit by `GET /api/knowledge/documents/{metadata.documentId}`. `POST /api/knowledge/scan` runs an immediate scan with an administrator credential; it accepts no caller-supplied filesystem path. Reads and searches require `knowledge.read`. All knowledge in this initial single vault is shared with agents having that permission; there are no per-document ACLs yet.

Search results include `type`, chunk ID, document title, Markdown section, cosine score, relative source path, document ID, heading hierarchy, original start/end line numbers, indexed revision token/content hash and indexing timestamp. `pathPrefix` matches a whole directory boundary or an exact document path; use forward slashes without a trailing slash. Scope/model/prefix filtering happens before the result limit. Limits range from 1 to 100.

Search checks candidates against current canonical files before returning them. Changed, deleted, linked or invalid source files are omitted. This may return fewer than the requested limit until re-indexing catches up. Returned sections identify the verified snapshot; a human edit immediately after a response can still make its token stale, as expected with optimistic concurrency.

Document reads always return the live canonical content, `path`, `revisionToken`, `contentHash`, `modifiedAt`, optional indexed ID and `indexCurrent`. An unindexed new document can be read by path. An indexed document that was edited returns the new content/token and `indexCurrent: false`; it does not serve old full text from PostgreSQL.

## Chunks and revisions

The initial chunker recognizes ATX headings (`#` through `######`) outside fenced code, preserves section context and original line numbers, and splits oversized sections/lines into chunks of at most 1,800 UTF-16 characters without splitting surrogate pairs. YAML frontmatter delimiters are excluded from embedding text when closed; raw frontmatter remains in document reads. Metadata parsing/enrichment and Setext heading support are not part of this chunker. Titles come from the first H1 or filename. Chunker version is explicit so future algorithm changes trigger re-indexing.

Revision tokens have the form `v1.<sha256>` and bind the exact vault-relative path to the SHA-256 hash of raw file bytes. BOM and newline changes count as revisions; touching only a timestamp does not. Tokens are opaque version identifiers, not authentication credentials, signatures or permission to write. The proposal/writer flow in Milestone 3 uses this same revision contract.

## Paths and safety

Paths must be relative `.md` paths using `/`. Absolute paths, traversal segments, alternate streams, backslashes, control characters, dot directories, ambiguous trailing dots/spaces and nonportable path characters are rejected. The reader checks each component for symlinks/reparse points; scans skip linked files/directories, including cycles, and dot directories such as `.git`/`.obsidian`. A linked configured root is rejected. Files must be valid UTF-8 and fit `Knowledge:MaximumDocumentBytes` (default 2 MiB; configurable up to 64 MiB).

The vault must be managed by trusted local operators and mounted read-only to the API. Only the worker writer receives write access. Portable path/component checks protect API traversal and ordinary link escapes; they are not an OS sandbox against a hostile local process continuously swapping filesystem objects. The writer repeats path and revision checks at application time.

## Schema and audit

The `KnowledgeIndex` EF migration adds:

- `knowledge_documents`: UUID, unique relative path, title, SHA-256, revision token, file/index times, embedding identity/dimensions and chunker version.
- `knowledge_chunks`: UUID, document FK with cascade deletion, unique document/ordinal, heading, section text, start/end lines and `vector(384)`.

Audit actions include `brain.knowledge.read`, `brain.knowledge.searched`, `brain.knowledge.indexed`, `brain.knowledge.removed`, `brain.knowledge.scanned`, and existing authorization denials. They record resource paths and scan counts without copying document content or queries into audit.

The development embedding provider is still token hashing, not a learned semantic model. PostgreSQL performs real cosine vector ranking, but paraphrase recall requires a production provider. Search compares matching embedding models only. Model changes re-embed on the next successful scan.
