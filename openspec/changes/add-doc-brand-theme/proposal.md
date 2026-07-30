## Why
The foundation already applies the DockYARP identity (palette, logo, favicon, typography) as Docsy overrides.
Two gaps remain for a production-quality brand theme: the site still pulls **Google Fonts from a CDN**
(a privacy/offline/GitHub-Pages concern), and light/dark contrast has not been checked against WCAG AA.

## What Changes
- **Self-host the fonts**: bundle Space Grotesk (400/500/600/700) and JetBrains Mono (400/500/700) as `woff2`
  under `docs-site/static/fonts/`, with local `@font-face` rules; disable Docsy's Google Fonts CDN and remove
  the remote `@import`. Include the fonts' OFL licenses.
- **Accessible contrast**: verify and adjust foreground/background pairs (body, muted text, links, code) so
  they meet WCAG AA in **both** light and dark modes.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the docs site self-hosts its fonts (no CDN) and meets WCAG AA contrast in both themes.

## Impact
- **Files**: `docs-site/static/fonts/*.woff2` + `LICENSES.md`; new `assets/scss/_fonts_project.scss`
  (`@font-face`); `_variables_project.scss` (`$td-enable-google-fonts: false`), `_styles_project.scss`
  (import fonts, drop the remote `@import`, contrast tweaks).
- **Verification (local, no E2E)**: `hugo serve` renders with the self-hosted fonts (no network font request)
  and correct contrast in light and dark. The user runs the local preview; no CI/e2e needed.
- **Owning agent**: AG-DOC. Resolves `add-doc-brand-theme`.
