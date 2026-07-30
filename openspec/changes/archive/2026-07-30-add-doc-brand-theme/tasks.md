## 1. Self-hosted fonts (AG-DOC)
- [x] 1.1 Fetch Space Grotesk (400/500/600/700) + JetBrains Mono (400/500/700) `woff2` (latin + latin-ext) into
      `docs-site/static/fonts/` (14 files, ~330 KB)
- [x] 1.2 New `assets/scss/_fonts_project.scss` with `@font-face` rules using `url("../fonts/…")`
- [x] 1.3 `_variables_project.scss`: `$td-enable-google-fonts: false`; `_styles_project.scss`: import the fonts
      partial and remove the remote `@import` (no CDN font request)
- [x] 1.4 Bundle the OFL licenses (Space Grotesk, JetBrains Mono) + a `LICENSES.md` attribution under
      `docs-site/static/fonts/`

## 2. Accessible contrast (AG-DOC)
- [x] 2.1 WCAG AA verified for body / muted / links / code in **both** light and dark — all 8 key pairs pass
      (lowest 4.76:1); no palette change needed

## 3. Verify (AG-DOC)
- [ ] 3.1 Local `hugo serve`: fonts load from `/fonts/` (no external font request), readable in light and dark
      — user to confirm in the local preview
- [x] 3.2 .NET `Nuke Test` gate unaffected (no code touched)
