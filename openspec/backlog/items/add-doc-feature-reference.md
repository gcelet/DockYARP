---
id: add-doc-feature-reference
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal initiative — documentation content gap)
provenance: 2026-07-31 (doc site added after most features; content lags the implementation)
---

## Why
The Hugo/Docsy site (`docs-site/`) was scaffolded **after** most of DockYarp was implemented, so its content is
still a thin starter set while the feature surface is large. Users need the site to document **everything
DockYarp can do and how** — the full label/env/config reference, per capability, matching the shipped behavior.

## nginx-proxy behavior
N/A — internal documentation initiative. No `parity.md` row.

## DockYarp today
- `docs-site/` has Getting Started, Configuration (a starter label/config table), Architecture, Deployment,
  Contributing. It does not yet cover the full, current feature set in depth (TLS policy/CERT_NAME/default-cert
  trust, host-network/multi-network/ContainerFilters, LB policies, vhost overrides, access-log fields, the
  `/api/resolve` admin endpoint, etc.).

## Proposed change (sketch)
- Write a complete **feature/configuration reference** on the site, derived from the archived `openspec/specs/`
  and the label/config surface: every container label (and, once `add-env-var-config` lands, env var), every
  app-config section (`Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`, `Compression`),
  with behavior, defaults, and examples.
- Organize by capability; cross-link Getting Started → reference. Keep it in sync as changes archive (add a
  "update docs" reminder to the change lifecycle for user-facing changes).

## Acceptance criteria (→ scenarios)
- **WHEN** a user browses the site **THEN** every implemented label/config option is documented with behavior +
  a correct example (real names, no placeholders).
- **WHEN** a capability is spec'd in `openspec/specs/` **THEN** the site reference covers it.

## Notes / risks / references
- Depends on [[add-doc-site-foundation]] (done). Best done after `add-env-var-config` so the reference covers
  env + label uniformly. Consider generating parts from the specs to reduce drift.
- Sibling doc items: [[add-doc-search]] (done), [[add-doc-brand-theme]] (done), [[add-doc-versioning]],
  [[add-doc-ci-publish]].
