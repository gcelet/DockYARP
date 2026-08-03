---
id: add-doc-capability-reference
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal — site content gap)
provenance: 2026-08-03 (split from add-doc-feature-reference; that pass covered labels + env vars)
---

## Why
`add-doc-feature-reference` documented the container **label/env** surface. The site still lacks a
per-capability reference for the **proxy's own application configuration** and the runtime features, and its
existing pages have not been audited against the shipped behavior + `openspec/specs/`.

## Scope
- **Audit** the whole site (`docs-site/`) against `openspec/specs/` + `openspec/backlog/parity.md`; list and
  fill every gap (features present in the product but missing/outdated on the site).
- **Application-config reference** per section: `Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`,
  `AdminApi`, `Compression`, `DataProtection`, `Server` (HTTP/HTTPS ports) — behavior, defaults, examples.
- **Runtime/feature docs**: the admin API (`/api/resolve`, metrics/`/metrics`), access-log fields/formats,
  host-network / multi-network discovery, `DOCKER_CONTAINER_FILTERS`, load-balancing policies, error pages,
  static (file-based) configuration.

## Acceptance criteria (→ scenarios)
- **WHEN** a capability is spec'd in `openspec/specs/` **THEN** the site documents it with behavior + a real example.
- **WHEN** the site is audited **THEN** no shipped feature is missing or described with stale behavior.

## Notes / risks / references
- Follows [[add-doc-feature-reference]] (labels + env, done). Consider generating parts from the specs to reduce
  drift. Sibling: [[add-doc-examples]]. Large; may split per capability.
