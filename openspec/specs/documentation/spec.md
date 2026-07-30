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
