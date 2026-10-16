# Vault metadata enrichment

Milestone 6 scans canonical Markdown, detects frontmatter issues, and generates
**pending** metadata proposals. The scanner has only read access through
`IKnowledgeVault`. It never edits a Markdown file. Existing proposal preview,
versioned review, writer access checks, revision tokens, durable Git preparation,
and the single Brain Writer apply approved changes.

## Schema

The default [version 1 schema](schemas/frontmatter-v1.schema.json) requires only
`id`, `title`, and `metadata_version`. Other fields are optional. This permits
minimal human-authored notes without requiring semantic classification.

| Field | Rule |
| --- | --- |
| `id` | Lowercase letters/digits/hyphens, 1–100 characters, leading letter/digit |
| `title` | Nonblank text, at most 300 characters |
| `metadata_version` | Integer equal to configured current version |
| `type` | One of the configured document types |
| `summary` | Nonblank text, at most 1,000 characters |
| `tags`, `domains`, `systems`, `services`, `projects`, `related_concepts` | Up to 30 distinct, nonblank strings, at most 100 characters each |
| `environment` | Nonblank single-line text, at most 100 characters |
| `metadata_generated` | Boolean; indicates generated fields, not ownership of all metadata |
| `path`, `filename` | Match the current canonical relative path and filename |
| `content_hash` | SHA-256 of the Markdown body excluding frontmatter |
| `created_at`, `modified_at` | Parseable date/time strings recording source provenance |
| `git_revision` | A 40–64 character lowercase hexadecimal source commit reference |

Default types are `procedure`, `architecture`, `service`, `incident`, `decision`,
`project`, `reference`, `note`, and `runbook`. Configure `Metadata:DocumentTypes`
in both API and worker to change them. Configure `Metadata:SchemaVersion` to
advance the schema. A hash also records the type policy and generator version,
so changing the schema policy invalidates scan caches and old patches. Configure
both hosts identically; a writer refuses a patch generated under another schema.
The JSON Schema file describes defaults; the runtime validator additionally
checks nonblank text, control characters, canonical path/hash matches and policy.

Unknown human fields, including nested mappings/lists, are preserved. Known
fields are type checked: unquoted YAML booleans/numbers are not string values.
YAML anchors, aliases, explicit tags, duplicate keys, complex keys, excessive
nesting, oversized frontmatter, missing closing delimiters, malformed YAML and
flow-style root mappings are reported for manual repair. They are never
replaced by guessed metadata. Frontmatter is limited to 65,536 characters,
4,096 parser events and 16 nesting levels. The existing vault path, link,
encoding and document-size limits still apply.

## Generation and preservation

For documents needing repair, deterministic enrichment fills missing/invalid:

- IDs derived from a filename slug and a path hash, so equal filenames in
  different directories receive different stable generated IDs.
- Titles derived from filenames without consulting a model.
- Canonical filename/path, body hash and source modification time.
- The current metadata version and a generated-fields marker.
- A source Git revision and latest Git add timestamp when available through
  read-only Git commands. No creation date is invented when history is unknown.

Existing valid human fields always win, including titles, classification, tags,
summaries, unknown fields, and `metadata_generated: false`. Generated suggestions
cannot overwrite them. This also applies when a document already carries
`metadata_generated: true`; that flag does not make all its fields disposable.
Optional semantic fields that are invalid and cannot be safely repaired without
a semantic suggestion remain visible as failures for manual repair. The scanner
does not delete them or invent a classification.

Proposals store a structured JSON patch, not model-supplied YAML. Brain validates
each field, renders values using escaped JSON scalar/array syntax (a YAML subset),
and changes only affected value spans or appends missing fields. Existing comments,
styles and unknown fields survive; the Markdown body remains byte-equivalent
after UTF-8 encoding. The writer preserves the source BOM and newline content.
A complete reparse and schema validation must pass before a proposal is queued
and again when the writer prepares it. Already-valid fields cannot be changed by
a patch. Preview shows the exact resulting document.

`content_hash` deliberately covers the body rather than the whole file: hashing
a frontmatter field into itself would create a regeneration loop. The database
separately stores the full canonical file hash and revision token. Valid existing
timestamps and Git references record the source snapshot when generated and are
not compared to each metadata commit's newer timestamp/revision. An applied
metadata patch therefore does not create another proposal just because its own
Git commit changed file metadata. A later body edit invalidates a managed body
hash and can create a new proposal.

## Scan state and review

`metadata_scan_states` stores one record per path: full source hash/revision,
schema version, policy fingerprint, checked/enriched timestamps, provider/model,
issue details, last error and linked proposal ID. A PostgreSQL advisory lease
excludes concurrent scans. Scan state, a new proposal and its generation audits
commit in one transaction. A crash cannot leave a proposal without its dedupe
state. An unchanged successfully inspected source/policy is skipped, including
while its proposal is pending or rejected. To reconsider a rejected proposal,
change the document or enrichment policy; repeated scans do not recreate it.

Changed source content, schema/type policy, semantic mode, model or configured
endpoint invalidates the cache. Successful valid documents need no model call.
Provider failures are recorded and retried on a later scan; ambiguous YAML is
cached as an issue requiring manual repair. State for deleted files is pruned
only after a complete vault enumeration. Source revisions are checked again
after provider I/O, and stale approved proposals become conflicts at the writer.

There is **no auto-apply policy**. Confidence and provider identity are stored on
the proposal for review/future policy work, not authorization. Every generated
proposal starts pending, even at confidence 1. The scanner identity must be
enabled and hold `knowledge.read` plus `knowledge.propose`; grants are resolved
again after model I/O. The writer also rechecks proposer and reviewer access.

Development seeding adds `metadata-enrichment-worker` with those two permissions
and no bearer credential. Its grants are not reset if an operator revokes them.
The existing `willem-de-jong` administrator reviews its proposals. Production
operators must provision an enabled scanner identity and grants themselves.

## APIs

All endpoints retain the existing bound agent credentials.

| Route | Behavior |
| --- | --- |
| `GET /api/knowledge/metadata/issues?limit=100` | Administrative persisted issues/errors; maximum 1,000 |
| `POST /api/knowledge/metadata/scan` | Administrative immediate scan and pending proposal generation |
| `GET /api/knowledge/proposals?type=metadata_enrichment` | Existing queue filtered before pagination; optional status/limit |
| `GET /api/knowledge/proposals/{id}` | Proposal, structured patch, provenance, confidence and review state |
| `GET /api/knowledge/proposals/{id}/preview` | Current before/after content and revision compatibility |
| `POST /api/knowledge/proposals/{id}/approve` | Existing explicit versioned review |
| `POST /api/knowledge/proposals/{id}/reject` | Existing explicit versioned rejection |

Issue reads require `knowledge.read` + `knowledge.admin`; scanning also requires
`knowledge.propose`. Proposal ownership and admin visibility follow the existing
queue rules. `type=knowledge_change` filters out metadata proposals. Unsupported
filter values are rejected. Metadata proposals report `type` and `operation` as
`metadata_enrichment`, plus `metadataSchemaVersion`, `enrichmentProvider`,
`enrichmentModel`, nullable `enrichmentConfidence`, and `generatedBy`.

Generic HTTP/MCP proposal submission cannot manufacture a metadata enrichment
operation: these proposals must pass through the administrative scanner and
validated generator. The eight existing MCP tools are unchanged. Approval bodies
remain `{"version":0,"reason":"Reviewed metadata and preserved human fields"}`.
The applied proposal retains its Git commit reference.

Scan counts distinguish inspected documents, cached skips, newly inspected
documents with issues, proposed changes, failures and deleted state. The issues
endpoint is a persisted scan snapshot; rescan after manual fixes to refresh it.
Audit actions include `brain.metadata.scan.started`, `brain.metadata.issue.detected`,
`brain.metadata.enrichment.generated`, `brain.metadata.proposal.created`,
`brain.metadata.proposal.approved`, `brain.metadata.proposal.rejected`,
`brain.metadata.enrichment.applied`, scan completion and enrichment failures.
Existing generic writer conflict/defer/preparation audits still record failures.

## Worker and optional semantic provider

Enable scheduled scanning with `Metadata__Enabled=true` and optionally configure
`Metadata__AgentId`. `Worker__MetadataScanIntervalSeconds` defaults to 300. Compose
enables scheduled deterministic scanning, with semantic enrichment explicitly
disabled. Outside Compose, scheduled scanning defaults off; administrative scans
remain available. The API's vault mount stays read-only. The worker's existing
writer is the only canonical file mutator.

`IMetadataEnrichmentProvider` is independent of the embedding provider and vendor.
The included optional adapter accepts an operator-configured OpenAI-compatible
Chat Completions endpoint, including compatible local gateways. To opt in, set
these values in the process performing scans:

```text
Metadata__SemanticEnabled=true
Metadata__Semantic__Endpoint=http://localhost:11434/v1/chat/completions
Metadata__Semantic__Model=<operator-selected model>
Metadata__Semantic__ApiKey=<optional out-of-band credential>
```

The URL is the complete endpoint; no provider/model is silently selected. A
container's localhost refers to that container. Use your deployment's reachable
model address. Credentials belong in environment/secret configuration and are
never persisted in scan/proposal tables. No external request is made when
semantic enrichment is disabled. Enabling it sends a bounded 12,000-character
body excerpt, relative path, known valid metadata and allowed document types to
that endpoint. Keep sensitive vault content in an appropriately trusted provider.

The adapter requests JSON mode for compatibility. JSON mode does not guarantee
schema adherence; Brain validates independently, as described in the
[official structured-output documentation](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=chat).
Responses have a 30-second timeout and 64 KiB transport bound, with up to 16,384
characters of semantic JSON. Redirects are disabled. Refused/truncated/malformed
responses produce controlled errors; raw provider diagnostics and credentials
are not persisted. Provider text must be one JSON object with required numeric
`confidence` in 0–1 and only supported optional fields:

```json
{"type":"procedure","summary":"Deploying reviewed releases through Coolify.","tags":["deployment"],"systems":["coolify"],"confidence":0.93}
```

Unknown or duplicate properties, invalid types/classifications/lists, raw YAML,
code fences and invalid confidence are rejected. Semantic fields are normalized
and then merged only where existing metadata is missing or invalid. The provider
cannot set deterministic fields, permissions, identities, proposal approval, or
arbitrary frontmatter keys. New providers implement the same JSON-returning
interface and receive the same validation.

## Verification

```powershell
./scripts/Test-Metadata.ps1
./scripts/Test-Metadata.ps1 -ApplySampleMetadata
```

The first command scans and checks issues/pending proposals/preview without
approving a write. The second **explicitly approves** metadata for the sample
`Infrastructure/Proxmox/nacht-02.md`, leaving an applied proposal, canonical
frontmatter and a Git commit. It expects that sample to have a pending proposal;
an already-enriched sample is not changed again. Other generated proposals remain
pending. Use these scripts after the main development setup/test.

Automated tests cover required/optional schema rules, filename derivation,
comments/styles/nested human fields, Unicode/CRLF, malformed YAML, unsafe YAML
features, invalid scalar types, future/old schemas, body-hash stability,
structured model validation, confidence/provenance, unchanged/rejected skips,
content/schema/model refresh, scan leases, atomic database rollback, permissions,
provider-time revision changes, review, actual Git application and stale conflicts.
Provider HTTP tests use a fake message handler; no live model endpoint or paid API
was contacted during verification.
