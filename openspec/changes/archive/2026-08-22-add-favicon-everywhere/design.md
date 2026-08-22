## Context

See `proposal.md` and `openspec/backlog/items/add-favicon-everywhere.md` for the live-investigated findings
(not re-derived here, including the full correction chain). The headline finding, discovered mid-change: the
docs-site's `favicons.html` partial hardcoded literal `/favicons/...` hrefs, ignoring the site's real
`baseURL` subpath (`https://gcelet.github.io/DockYARP/`) — every favicon link 404s in actual production. Also
genuinely missing (confirmed via `git ls-files`, not a keyword search): `favicon.ico`, Android/PWA icons,
`site.webmanifest`. The admin dashboard has no favicon reference at all and no static-file serving.

## Goals / Non-Goals

**Goals:**
- Fix the docs site's broken/incomplete icon set (apple-touch-icon, `.ico`, Android/PWA icons + manifest).
- Add a favicon to the admin dashboard without introducing static-file serving it doesn't otherwise need.

**Non-Goals:**
- Redesigning the icon itself — the existing `assets/favicon/favicon.svg` mark is unchanged, only its
  derived/raster forms and wiring are completed.
- Adding a general-purpose static-asset pipeline to the dashboard for this one file.

## Decisions

**`favicons.html`'s hrefs are routed through Hugo's `relURL`, not hardcoded literal strings — the fix for the
actual root-cause bug.**

Rationale: `relURL` is Hugo's own mechanism for producing a baseURL-subpath-aware relative URL from a
site-root-relative path; the theme's own default `favicons.html` already uses it correctly (confirmed by
reading it) — the project's override simply hadn't. No alternative considered: this is the standard, idiomatic
Hugo fix for exactly this class of bug, not a judgment call.

**`site.webmanifest`'s icon paths are relative (no leading slash), not passed through Hugo templating.**

Rationale: `static/` files are copied verbatim by Hugo, never templated — `relURL` isn't available inside
`site.webmanifest` without converting it to a custom Hugo output format (real complexity for one file). The Web
App Manifest spec resolves `icons[].src` relative to the *manifest's own URL*, so a relative path
(`favicons/android-chrome-192x192.png`) is correct under any baseURL subpath automatically, with zero
templating — verified live (curled the manifest and its referenced icons at the real subpath-prefixed URL,
both 200).

**docs-site keeps real static files (existing pattern); the dashboard uses an inlined `data:` URI (new, but
minimal) — not the same mechanism for both.**

Rationale: the docs site already has a working static-favicon convention (`docs-site/static/favicons/` +
Hugo's `favicons.html` partial) — completing it is a pure continuation of that pattern, not a new decision. The
dashboard has deliberately shipped with no static-file serving at all (see `openspec/specs/admin-api/spec.md`'s
"Read-only admin dashboard" requirement: "ships with no external CDN dependency and no JavaScript framework").
Adding `UseStaticFiles`/`wwwroot` purely to serve one small icon would be a disproportionate infrastructure
change for this fix; the source SVG is tiny (475 bytes) and self-contained (no external font/image references),
so inlining it as a `data:image/svg+xml` URI directly in the existing single-file `Index.cshtml` keeps the
dashboard's existing "fully self-contained page" shape intact.

**Android/PWA icons + `site.webmanifest` are hand-added; Docsy's own tooling doesn't produce them.**

Rationale: confirmed by reading `gen-favicons`' own README and the theme's `favicons.html` partial — both are
scoped to `favicon.ico`/`favicon-NxN.png`/`apple-touch-icon(-NxN).png` only. A manifest is a separate,
unrelated concern (PWA install metadata, not favicon rendering) that Docsy doesn't opinionate on, so it's
added directly to the project's existing `favicons.html` partial override rather than waiting on/expecting the
theme to grow this feature.

**ImageMagick-dependent generation (`.ico`, Android raster sizes) is a separate task boundary from the
zero-dependency fixes (copying the existing `favicon-180.png` into place, the dashboard data URI).**

Rationale: this change was proposed while ImageMagick was not yet installed on the implementing machine; the
zero-dependency tasks are not blocked on it and should not wait for it.

## Risks / Trade-offs

- [Risk] The rasterized Android/`.ico` icons are only as good as the source SVG at small sizes (a 26px corner
  radius on a 120×120 viewBox scaled down to 16px may look different than intended). → Accepted: same source
  mark already in production use for the existing 16/32px docs-site icons; not a new risk this change
  introduces.
- [Risk] `gen-favicons` may produce filenames/sizes that don't exactly match what's hardcoded in the project's
  `favicons.html` partial (e.g. if invoked with non-default `--apple`/`--png` sizes). → Mitigation: run it with
  its documented defaults (180 for apple, 16/32 for png, 16/32/48 for ico) which match what the partial already
  references, and diff the partial against the actual generated files before considering the task done.
