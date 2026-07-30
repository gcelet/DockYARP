---
id: add-doc-site-foundation
capability: documentation
agent: AG-DOC
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user idea 2026-07-30 (doc-site family, foundation-first split of add-doc-site)
---

## Why
DockYARP's documentation lives only as scattered Markdown in the repo (OpenSpec, architecture notes, module
READMEs). There is no structured, navigable documentation site, which hurts onboarding and prevents publishing
a professional portal like modern infra projects (Kubernetes, Istio, Envoy). This item is the **keystone** of
the doc-site family: a working Hugo + Docsy site with the base information architecture that every other
doc-site item builds on.

## nginx-proxy behavior
N/A — internal initiative. No `parity.md` row (documentation infrastructure is not an nginx-proxy feature gap).

## DockYarp today
- Markdown under `docs/`, `openspec/`, and module-level READMEs; no static site generator, theme, or nav.

## Proposed change (sketch)
- Add a `docs-site/` Hugo project using the Docsy theme (Hugo **Extended** required). Keep it isolated from the
  application build.
- Define the information architecture / nav sections: Getting Started, Architecture, OpenSpec, Modules,
  Deployment, Backlog.
- Wire ingestion of existing Markdown (`docs/`, `openspec/`, module READMEs) so authored content surfaces in the
  site (mounts / content adapters), and new files appear without bespoke wiring.
- Minimal homepage + working left-nav/sidebar. A single local build command produces the static site.

## Acceptance criteria (→ scenarios)
- **WHEN** the site is built locally **THEN** a complete static site is generated from the repo Markdown with a
  working nav/sidebar and the defined section hierarchy.
- **WHEN** a new Markdown file is added under an ingested path **THEN** it appears in the site without extra config.
- **WHEN** Hugo Extended + Docsy are absent **THEN** the build fails with a clear, actionable message.

## Notes / risks / references
- Docsy requires Hugo **Extended**; pin/validate versions. Validate Docsy licensing.
- Prerequisite for [[add-doc-brand-theme]], [[add-doc-search]], [[add-doc-versioning]], [[add-doc-ci-publish]].
- CI build + publication are intentionally **out of scope here** (see [[add-doc-ci-publish]]); this item is a
  local, deterministic scaffold.
