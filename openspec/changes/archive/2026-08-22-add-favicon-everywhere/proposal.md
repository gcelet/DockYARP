## Why

The docs site's favicon has very likely **never actually rendered in production**: `docs-site/layouts/partials/
favicons.html` hardcoded literal `/favicons/...` hrefs, ignoring the site's configured GitHub Pages subpath
(`baseURL = "https://gcelet.github.io/DockYARP/"`) — verified live that these exact links 404 at the real
served URL, while the correctly-prefixed `/DockYARP/favicons/...` path serves the file. This is the actual
root cause behind the user's original report, found only after an earlier "already correct, nothing to fix"
conclusion in this same investigation turned out to itself be wrong (tested a guessed URL, not the literal
href the HTML emits) — see `openspec/backlog/items/add-favicon-everywhere.md` for the full chain of findings
and corrections, now also a standing lesson ([[verify-negative-findings]]). Separately, `favicon.ico` and
Android/PWA icons were genuinely missing (confirmed via `git ls-files`), and the admin dashboard had no
favicon reference at all.

## What Changes

- docs-site: fix `favicons.html` to route every href through Hugo's `relURL` instead of hardcoded literal
  strings — the actual bug, affecting every icon link and the new manifest link alike.
- docs-site: generate the missing `favicon.ico` via the project's already-vendored Docsy `gen-favicons` tool
  (ImageMagick-backed). The existing svg/16px/32px/apple-touch-icon PNG set was confirmed byte-correct and is
  not regenerated.
- docs-site: add Android/PWA icons (192×192, 512×512) + a `site.webmanifest` (relative icon paths, resolved
  against the manifest's own URL per the Web App Manifest spec — correct under the subpath with no Hugo
  templating needed) + the `<link rel="manifest">` tag.
- Admin dashboard (`src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`): add a favicon via an inlined
  `data:image/svg+xml` URI in `<head>` — no static-file serving exists or is being introduced for this,
  consistent with the dashboard's existing dependency-free, single-file-page design.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — this is an asset/markup completeness fix with no testable behavior change; `skip_specs: true` is set
in this change's `.openspec.yaml`, matching the project's carve-out for changes with no spec-level behavior)

## Impact

- `docs-site/static/favicons/` — new files (`favicon.ico`, `android-chrome-192x192.png`,
  `android-chrome-512x512.png`); `docs-site/static/site.webmanifest` — new file.
- `assets/favicon/` — kept as the canonical source; new files mirrored here too so the two don't drift.
- `docs-site/layouts/partials/favicons.html` — rewritten to use `relURL` for every href (the actual bug fix),
  plus the new `favicon.ico` and `<link rel="manifest">` entries.
- `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml` — one new `<link rel="icon">` tag in `<head>`.
- `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml` — one new `<link rel="icon">` tag in `<head>`.
- No `DockYarp.slnx` build impact expected (dashboard change is a markup-only edit); `dotnet build` still run
  to confirm no Razor compilation regression.
