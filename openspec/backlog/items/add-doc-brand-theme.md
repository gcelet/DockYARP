---
id: add-doc-brand-theme
capability: documentation
agent: AG-DOC
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user idea 2026-07-30 (doc-site family)
---

## Why
A documentation portal should carry a recognizable DockYARP identity rather than the stock Docsy look. A brand
theme (colors, logo, typography) makes the site feel like a product portal and improves readability and trust.

## nginx-proxy behavior
N/A — internal initiative. No `parity.md` row.

## DockYarp today
- No DockYARP visual identity is applied to documentation (the foundation site uses default Docsy styling).

## Proposed change (sketch)
- Define the DockYARP charte graphique: brand palette (light + dark), logo/wordmark and favicon, heading/body
  typography, and accent colors for admonitions/links.
- Apply it as Docsy overrides (SCSS variables, partial/layout overrides, static assets) — no fork of the theme.
- Ensure accessible contrast (WCAG AA) in both light and dark modes.

## Acceptance criteria (→ scenarios)
- **WHEN** the site is built **THEN** it renders with the DockYARP palette, logo, favicon, and typography in both
  light and dark modes.
- **WHEN** foreground/background pairs are checked **THEN** they meet WCAG AA contrast.

## Notes / risks / references
- Depends on [[add-doc-site-foundation]] (theme overrides target the Docsy scaffold).
- Keep overrides in `docs-site/` assets; do not vendor a modified Docsy copy.
