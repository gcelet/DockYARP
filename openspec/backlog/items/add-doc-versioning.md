---
id: add-doc-versioning
capability: documentation
agent: AG-DOC
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user idea 2026-07-30 (doc-site family)
---

## Why
Once DockYARP ships released versions, users on an older version need docs that match it. Documentation
versioning (a version switcher + per-version content) keeps each release's docs accurate instead of only ever
showing `main`.

## nginx-proxy behavior
N/A — internal initiative. No `parity.md` row.

## DockYarp today
- Single, unversioned documentation (the foundation site publishes only current content).

## Proposed change (sketch)
- Add a documentation version switcher (Docsy `version`/`versions` config) exposing at least `latest` plus
  released versions (e.g. `v1`).
- Define how a version is cut (branch/tag or content directory) and how the switcher URL maps to it.
- Show a clear banner when viewing anything other than the latest version.

## Acceptance criteria (→ scenarios)
- **WHEN** multiple versions are configured **THEN** the site shows a version switcher and serves each version's
  content under its own path.
- **WHEN** a non-latest version is viewed **THEN** a banner indicates it is not the latest and links to latest.

## Notes / risks / references
- Depends on [[add-doc-site-foundation]]. Best done once DockYARP stabilizes / cuts a first release.
- Decide the versioning source (git branches/tags vs content dirs) before implementing.
