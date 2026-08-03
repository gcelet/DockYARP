---
id: add-doc-runtime-reference
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal — site content gap)
provenance: 2026-08-03 (split from add-doc-capability-reference; that pass did the application-config reference)
---

## Why
`add-doc-feature-reference` (labels + env) and `add-doc-capability-reference` (application config) documented the
**configuration** surface. The site still lacks a **runtime-feature narrative** and has not been audited against
the shipped behavior + `openspec/specs/`.

## Scope
- **Audit** the whole site (`docs-site/`) against `openspec/specs/` + `openspec/backlog/parity.md`; list and fill
  every gap (features present in the product but missing/outdated on the site).
- **Runtime-feature docs** (behavior + examples): the admin API endpoints (`/api/resolve`, `/metrics`),
  access-log fields/formats, host-network / multi-network discovery, `DOCKER_CONTAINER_FILTERS`, load-balancing
  policies, custom error pages, static (file-based) configuration, graceful shutdown.

## Acceptance criteria (→ scenarios)
- **WHEN** a capability is spec'd in `openspec/specs/` **THEN** the site documents it with behavior + a real example.
- **WHEN** the site is audited **THEN** no shipped feature is missing or described with stale behavior.

## Notes / risks / references
- Follows [[add-doc-feature-reference]] + [[add-doc-capability-reference]] (done). Sibling: [[add-doc-examples]].
  Consider generating parts from the specs to reduce drift.
