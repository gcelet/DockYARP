## 1. Zero-dependency fixes (AG-DOC)

- [x] 1.1 **Correction found live**: this task originally planned to copy `assets/favicon/favicon-180.png` →
      `docs-site/static/favicons/apple-touch-icon-180x180.png` to fix an assumed 404. Investigation before
      implementation found the premise was wrong: the file was already git-tracked and byte-identical (see
      `git ls-files`/`git log` for the path — committed 2026-07-30, untouched). The original claim came from a
      `find -iname "*favicon*"` search that structurally cannot match a filename without "favicon" in it
      (`apple-touch-icon-180x180.png`). No fix was needed or applied here.
- [x] 1.2 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`: added
      `<link rel="icon" type="image/svg+xml" href="data:image/svg+xml,...">` to `<head>` — angle brackets and
      quotes left unescaped (single-quoted attributes) for readability/diffability, only `#` percent-encoded
      (`%23`, required since `#` is a URI fragment delimiter), matching `assets/favicon/favicon.svg`'s exact
      content. Verified: `dotnet build DockYarp.slnx` — 0 warnings/errors.
- [x] 1.3 Ran the docs site locally for real (`npm run serve` from `docs-site/`) and curled the live dev
      server: `favicon.svg`/`favicon-32x32.png`/`favicon-16x16.png`/`apple-touch-icon-180x180.png` all return
      200 — confirms task 1.1's finding (nothing was ever broken here) and that this change doesn't regress it.
- [x] 1.4 Ran the real app locally (`dotnet run --project src/DockYarp.App`, `AdminApi:Surface=ApiAndDashboard`,
      scratch `Tls:CertificateDirectory` per [[smoke-test-scratch-dirs]]) and curled `/dashboard` for real: the
      new `<link rel="icon">` tag is present in the served HTML.

## 1a. Real root cause found (not in the original plan) — the docs site's favicon links ignore the site's
##     baseURL subpath (AG-DOC)

- [x] 1a.1 **Significant correction, found live while re-verifying task 1.1's "already correct" conclusion
      against the site's real production URL shape.** `hugo.toml`'s `baseURL = "https://gcelet.github.io/DockYARP/"`
      — a GitHub Pages *project* site, served under a `/DockYARP/` subpath, not the domain root. Confirmed via
      the actual dev server (which mounts at `http://localhost:PORT/DockYARP/`, mirroring the configured
      baseURL) that `docs-site/layouts/partials/favicons.html`'s hardcoded literal hrefs (`/favicons/favicon.svg`,
      etc. — no subpath prefix) **return 404** when requested at the exact URL the HTML emits; only the
      `/DockYARP/`-prefixed path serves the file. Task 1.1's "verified working, nothing broken" conclusion was
      itself testing the wrong URL (a guessed `/DockYARP/`-prefixed path, not the literal href the HTML actually
      emits) — a second instance of the same [[verify-negative-findings]] lesson: even a "confirmed working"
      claim needs to test the *exact* thing referenced, not an assumed variant of it. This is very likely the
      **actual root cause** of the favicon never appearing in production that the user originally reported —
      more significant than any file-existence gap this change originally set out to fix.
- [x] 1a.2 `docs-site/layouts/partials/favicons.html`: rewrote to pass every href through Hugo's `relURL`
      (`{{ "favicons/favicon.svg" | relURL }}`, etc.) instead of hardcoded literal strings — `relURL` correctly
      prepends the configured baseURL's path segment. Also added the now-generated `favicon.ico` link (was
      never linked at all before this change) and the `<link rel="manifest">` tag (folded in here since it's
      the same file/mechanism, ahead of task 2.6 below).
- [x] 1a.3 Re-verified for real against a fresh dev server + the actual `npm run build` production output
      (`docs-site/public/index.html`): every emitted href now correctly reads `/DockYARP/favicons/...` /
      `/DockYARP/site.webmanifest`, and every one of the 8 resulting URLs (6 icon links + manifest + its 2
      manifest-referenced icons) returns 200 when curled at that exact path.

## 2. ImageMagick-dependent generation (AG-DOC)

- [x] 2.1 Confirmed ImageMagick 7.1.2-Q16-HDRI installed (`magick -version`) — not on PATH for the running
      session (installed after the shell started), worked once invoked with the full install path prepended to
      `PATH` for the command.
- [x] 2.2 Ran the vendored Docsy `gen-favicons` CLI
      (`node docs-site/themes/docsy/theme/scripts/gen-favicons/cli.mjs --png none --apple none
      static/favicons/favicon.svg static/favicons/`, run from `docs-site/` — the source path had to be the
      site's own `static/favicons/favicon.svg` copy, not the repo-root `assets/favicon/favicon.svg` initially
      guessed, since the CLI resolves the source path relative to its own invocation directory) — wrote
      `favicon.ico` (new), leaving the already-correct PNG/apple-touch-icon set untouched as planned.
- [x] 2.3 Mirrored `favicon.ico` into `assets/favicon/` (the canonical source folder).
- [x] 2.4 Generated `android-chrome-192x192.png`/`android-chrome-512x512.png` via
      `gen-favicons --png 192,512 --ico none --apple none static/favicons/favicon.svg static/favicons/`,
      renamed from the tool's own `favicon-192x192.png`/`favicon-512x512.png` output to the `android-chrome-`
      prefix, mirrored into `assets/favicon/` too.
- [x] 2.5 New `docs-site/static/site.webmanifest`: `name`/`short_name` "DockYARP", `theme_color`/
      `background_color` `#0F1226` (matching the existing meta tag), `display: "standalone"`, and an `icons`
      array using **relative** paths (`favicons/android-chrome-192x192.png`, no leading slash) — the Web App
      Manifest spec resolves `icons[].src` relative to the manifest's own URL, so a relative path is correct
      under any baseURL subpath with zero Hugo templating needed (the manifest itself is an untemplated
      `static/` passthrough file, so `relURL` isn't available inside it).
- [x] 2.6 Manifest link folded into task 1a.2 above (same file, same fix).
- [x] 2.7 Verified for real (task 1a.3's dev-server + production-build checks already covered this): manifest
      returns 200, both `android-chrome-*` sizes it references return 200 at the correct subpath-prefixed URL.

## 2a. Blurry raster output found post-implementation, by the user (AG-DOC)

- [x] 2a.1 **Real defect reported by the user after this change was presented as complete**: the 192/512
      Android icons rendered visibly blurry — confirmed by actually viewing the generated PNG, not just
      trusting `gen-favicons`' exit code. Root-caused: `assets/favicon/favicon.svg` declared
      `width="32" height="32"` alongside `viewBox="0 0 120 120"`. ImageMagick's SVG delegate rasterizes at the
      declared *width/height* (32×32 — confirmed via `magick identify`, which reported `SVG 32x32` for the raw
      file), not the viewBox, then `-resize` bitmap-upscales that small raster — a 16× upscale to 512px, hence
      the visible blur. The 16×16/32×32 targets were unaffected (no upscaling: native 32px source ≥ those
      targets) which is why the original per-file spot check (task 1.3/2.7) didn't catch it — those checks
      confirmed HTTP 200 and file presence, never actually opened the image to look at it.
- [x] 2a.2 Fix: changed `assets/favicon/favicon.svg`'s declared `width`/`height` from `32`/`32` to `120`/`120`,
      matching its own `viewBox` — verified via a scratch render (120×120 native → 512px upscale, 4.3×) that
      this alone resolves the blur, before touching any tracked file. Synced the corrected SVG to
      `docs-site/static/favicons/favicon.svg` and to the dashboard's inlined `data:` URI in `Index.cshtml`
      (same source content, same latent issue, even though browsers rendering an SVG favicon directly — not
      through ImageMagick — likely weren't actually affected; fixed for consistency regardless).
- [x] 2a.3 Regenerated the **entire** icon set from the corrected source (`gen-favicons --ico 16,32,48 --png
      16,32,192,512 --apple 180`), not just the two Android sizes — the pre-existing `favicon-16x16.png`/
      `favicon-32x32.png`/`apple-touch-icon-180x180.png` inherited from 2026-07-30 were generated the same way
      and had never been re-examined for sharpness; the 180px apple-touch-icon (a 5.6× upscale from the old
      32px native source) turned out to need the fix too, not just the two new Android sizes. Renamed the
      tool's own output filenames to match the project's established conventions
      (`apple-touch-icon.png`→`apple-touch-icon-180x180.png`, `favicon-192x192.png`→`android-chrome-192x192.png`,
      etc.), overwriting the blurry versions. Verified visually (viewed the regenerated 512×512 and 180×180
      PNGs directly, not just re-checked file presence) — both sharp, no visible upscale blur.
- [x] 2a.4 Also regenerated `assets/favicon/favicon-48.png` from the corrected source for consistency, even
      though it turned out to be unreferenced by any markup (confirmed via a repo-wide grep) — a stale blurry
      leftover otherwise.
- [x] 2a.5 Re-ran every verification this change already performed (real `npm run build`, a fresh dev server,
      curling all 7 favicon/manifest URLs at the correct `/DockYARP/`-prefixed path, and an `md5sum` diff
      between `assets/favicon/` and `docs-site/static/favicons/`) — all still pass with the corrected,
      sharp assets. `dotnet build DockYarp.slnx` re-run after the dashboard SVG edit — 0 warnings/errors.

## 3. Final validation (AG-DOC)

- [x] 3.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 3.2 `cd docs-site && npm run build` — Hugo build succeeds (42 static files, was 38 — the 4 new files),
      no broken-link warnings.
- [x] 3.3 Diffed `assets/favicon/` against `docs-site/static/favicons/` via `md5sum` on every shared file
      (`favicon.svg`, `favicon.ico`, `favicon-16`/`32`, `apple-touch-icon-180x180`/`favicon-180`,
      `android-chrome-192x192`/`512x512`) — all hashes match, no drift.
