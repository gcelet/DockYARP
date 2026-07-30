---
id: add-doc-search
capability: documentation
agent: AG-DOC
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user idea 2026-07-30 (doc-site family)
---

## Why
A documentation site is only useful if content is findable. Search lets users jump to the right page instead of
scrolling the nav, which matters as the doc set grows.

## nginx-proxy behavior
N/A — internal initiative. No `parity.md` row.

## DockYarp today
- No search over the documentation (the foundation site ships nav only).

## Proposed change (sketch)
- Enable client-side offline search by default (Docsy's built-in **Lunr.js** index) — zero external dependency,
  works on GitHub Pages and offline.
- Optionally support **Algolia DocSearch** behind configuration for a hosted, higher-quality index once the site
  is public; default stays Lunr so the site never depends on a third party to be searchable.

## Acceptance criteria (→ scenarios)
- **WHEN** a user types a query in the search box **THEN** matching pages are returned client-side (Lunr) with no
  external service.
- **WHEN** Algolia is configured **THEN** search uses the hosted index; **WHEN** it is not **THEN** the site falls
  back to Lunr.

## Notes / risks / references
- Depends on [[add-doc-site-foundation]].
- Lunr index size grows with the doc set; validate build time and index weight.
