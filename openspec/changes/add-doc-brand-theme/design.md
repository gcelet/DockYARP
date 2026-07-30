# Design — add-doc-brand-theme

## Context
The foundation set the brand palette, logo, favicon, and typography, but fonts load from the Google Fonts CDN
(`$td-enable-google-fonts: true` + a remote `@import` for JetBrains Mono). Docsy compiles `scss/main.scss`
without template processing, so the CSS cannot use Hugo functions — self-hosted fonts must be plain static
assets referenced by a path relative to the compiled CSS.

## Decisions

### 1. Self-hosted fonts as static assets
Bundle `woff2` files under `docs-site/static/fonts/` (served at `<baseURL>/fonts/`). The compiled CSS lives at
`<baseURL>/scss/main.css`, so `@font-face` uses `url("../fonts/<file>.woff2")` — a path **relative to the CSS**
that resolves correctly under any `baseURL` (including the GitHub Pages `/DockYARP/` subpath). Only the `latin`
and `latin-ext` subsets are bundled (English docs + accented names), keeping the payload small.

### 2. `@font-face` in a dedicated partial SCSS
The `@font-face` rules live in `assets/scss/_fonts_project.scss`, imported at the top of
`_styles_project.scss` (loaded after Bootstrap). `$td-enable-google-fonts` is set to `false` and the remote
JetBrains Mono `@import` is removed, so **no font request leaves the origin**. `$font-family-sans-serif` /
`$font-family-monospace` already reference the families.

### 3. Licensing
Both families are SIL OFL 1.1. Their license texts are bundled under `docs-site/static/fonts/` with a
`LICENSES.md` attributing each font (name, copyright, source, OFL 1.1).

### 4. Accessible contrast (WCAG AA)
Verify the key pairs in both themes and adjust muted/link colors if any fall below AA (4.5:1 for body text,
3:1 for large text): light body `#334155` on `#FFFFFF`, links `#7C3AED` on white; dark text `#E2E8F0` and
muted on `#0F1226`, links `#A78BFA` on dark. Adjust the dark muted tone if it is below AA.

## Verification
- **Local only (no e2e)**: `hugo serve`; confirm fonts load from `/fonts/` (no `fonts.gstatic.com` request in
  the network panel) and text/links/code read well in light and dark. The `.NET` gate is untouched.

## Risks
- Google Fonts `woff2` URLs are versioned; the fetch script records the source and the font version so the
  bundle can be regenerated deterministically.
