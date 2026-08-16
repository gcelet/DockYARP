---
id: add-doc-nginx-proxy-migration-guide
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal initiative — adoption/migration content, no parity row of its own)
provenance: 2026-08-16 user idea (session close) — depends on a write-up of the user's own installation, not yet provided
---

## Why
DockYarp positions itself as an nginx-proxy equivalent (`AGENTS.md`: "a dynamic reverse proxy for Docker
containers, an nginx-proxy equivalent"), and `openspec/backlog/parity.md` already tracks feature-by-feature
compatibility in depth — but that matrix is an **engineering tracking doc**, not something written for an
nginx-proxy operator deciding whether/how to switch over. Nothing on the doc site walks a reader through an
actual migration.

## nginx-proxy behavior
N/A — internal initiative (a migration guide for adopting DockYarp, not a proxy feature). No `parity.md` row.

## DockYarp today
- `openspec/backlog/parity.md` — the definitive feature matrix; this guide should **link to it**, not duplicate
  it, for the exhaustive comparison.
- `docs-site/content/en/docs/configuration.md` already documents the nginx-proxy-compatible label set
  (`VIRTUAL_*`, `LETSENCRYPT_*`, `CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`, `HSTS`, `NETWORK_ACCESS`,
  `SERVER_TOKENS`, `EXTERNAL_HTTPS_PORT`) and the `DOCKYARP_*` extensions — most of the label-level mapping work
  is already written, just not framed as "coming from nginx-proxy."
- `getting-started.md` / `examples.md` cover a fresh DockYarp install and recipes, not a migration path from an
  existing nginx-proxy deployment.

## Proposed change (sketch) — two paths, per the user's request
1. **Basic path**: a typical nginx-proxy + `acme-companion` docker-compose setup → the equivalent DockYarp
   compose stack. Mostly a direct label swap (most nginx-proxy labels already work unchanged per
   `configuration.md`) plus the compose-service-level changes (image, socket-proxy recommendation, etc.).
2. **Advanced path**: grounded in the **user's own production nginx-proxy installation** on their real network.
   **Blocked on user input**: the user will write a recap document describing their actual setup (topology,
   labels/features used, TLS arrangement, etc.) to serve as the source material for this half — do not draft
   the advanced path from assumptions before that recap exists.

## Acceptance criteria (→ scenarios)
- **WHEN** an operator running a basic nginx-proxy + acme-companion stack reads the guide **THEN** they get a
  direct, copy-pasteable compose/label translation to the DockYarp equivalent.
- **WHEN** an operator running an advanced/production-grade nginx-proxy setup (matching the patterns in the
  user's own recap) reads the guide **THEN** they find guidance for those specific advanced patterns, not just
  the basic case.
- **WHEN** the guide needs the exhaustive feature-by-feature comparison **THEN** it links to
  `openspec/backlog/parity.md` rather than restating it.

## Notes / risks / references
- **Do not start the advanced path (or propose this change) until the user's installation recap document
  exists** — it's the primary source material for that half; the basic path could in principle be drafted
  first/independently if the user wants to split this into two items later.
- Decide at propose-time: one doc-site page with two sections, or two separate pages (basic vs. advanced) —
  likely one page, mirroring how `examples.md` already groups multiple recipes on one page.
- Refs: `openspec/backlog/parity.md`, `docs-site/content/en/docs/configuration.md`,
  `docs-site/content/en/docs/getting-started.md`.
