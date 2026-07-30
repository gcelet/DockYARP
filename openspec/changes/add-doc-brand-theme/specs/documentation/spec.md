## ADDED Requirements

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
