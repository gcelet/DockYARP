# documentation Specification

## Purpose
The DockYARP documentation site — a Hugo + Docsy static site under `docs-site/` — and the requirements
governing its structure, theming, and content.

## Requirements
### Requirement: Documentation site scaffold
The project SHALL provide a Hugo + Docsy documentation site under `docs-site/`, isolated from the .NET build,
carrying the DockYARP brand identity. The site SHALL define an information architecture with navigable sections
(at least Getting Started, Configuration, Architecture, Deployment, Contributing) and SHALL render correctly in
both light and dark themes. Documentation content SHALL use DockYARP's real container labels
(`VIRTUAL_HOST`, `VIRTUAL_PORT`, `LETSENCRYPT_HOST`, `DOCKYARP_*`).

#### Scenario: Site builds from the repository content
- **WHEN** the site is built with Hugo Extended (Docsy resolved, dependencies installed)
- **THEN** a complete static site is generated from `docs-site/` with a working navigation and section hierarchy

#### Scenario: Both themes are supported
- **WHEN** the site is viewed in light or in dark mode
- **THEN** it renders with the DockYARP palette and readable contrast in either theme

#### Scenario: Content uses real labels
- **WHEN** a reader follows the Getting Started / Configuration content
- **THEN** the examples use `VIRTUAL_HOST`/`VIRTUAL_PORT`/`LETSENCRYPT_HOST`/`DOCKYARP_*`, not invented labels

### Requirement: Self-hosted fonts and accessible theming
The documentation site SHALL self-host its web fonts (no external font CDN request) and SHALL meet WCAG AA
contrast for body text, muted text, links, and code in both light and dark themes. The bundled fonts SHALL
carry their open-source license.

#### Scenario: Fonts load from the origin
- **WHEN** a page is loaded
- **THEN** its fonts are served from the site's own `/fonts/` path and no request is made to a font CDN

#### Scenario: Contrast meets WCAG AA in both themes
- **WHEN** the site is viewed in light or dark mode
- **THEN** body/muted/link/code foreground–background pairs meet WCAG AA contrast

### Requirement: Client-side documentation search
The documentation site SHALL provide client-side search over its content. By default it SHALL use an offline
Lunr index generated at build time, with the Lunr library self-hosted so no request is made to a CDN or search
service. Algolia DocSearch SHALL be supported as an opt-in alternative via configuration; when it is not
configured, the site SHALL fall back to the offline Lunr search.

#### Scenario: Offline search returns results without an external service
- **WHEN** a user types a query in the search box and Algolia is not configured
- **THEN** matching pages are returned client-side from the local Lunr index, with no request to a CDN or
  search service

#### Scenario: Algolia is used when configured
- **WHEN** `params.search.algolia` is configured
- **THEN** the site uses Algolia DocSearch instead of the offline Lunr search
