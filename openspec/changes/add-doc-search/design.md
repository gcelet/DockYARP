# Design — add-doc-search

## Context
Docsy provides offline search: when `params.offlineSearch` is set, it generates `offline-search-index.json` at
build time and runs queries client-side with a local `offline-search.js`. However, Docsy's `head.html`
hardcodes the **Lunr** library from `unpkg` (and **jQuery** from `code.jquery.com`) as CDN `<script>` tags —
there is no config param to change the source.

## Decisions

### 1. Enable offline search
Set `params.offlineSearch = true` (plus `offlineSearchSummaryLength` / `offlineSearchMaxResults` defaults). The
index and query engine are local; no query ever leaves the browser.

### 2. Self-host Lunr (and jQuery) via a `head.html` override
Since the CDN URLs are hardcoded in `head.html`, override it in the project (`layouts/_partials/head.html`) —
a faithful copy of Docsy's, with only the two `<script src>` swapped for local files under `static/js/`,
referenced with `relURL` so they resolve under the `/DockYARP/` baseURL. jQuery is loaded unconditionally by
Docsy, so self-hosting it in the same override removes the last CDN request from the head. The downloaded files
are verified against Docsy's SRI hashes so they are byte-identical to what the theme expected.

### 3. Keep Algolia as an opt-in alternative
The override copies Docsy's `algolia/head` define verbatim, so `params.search.algolia` still works. The default
remains Lunr; Algolia is documented as commented config. This satisfies "Algolia when configured, else Lunr".

### 4. Override maintenance
Overriding `head.html` couples it to Docsy v0.16's head. The Docsy submodule is pinned, so it will not drift
until deliberately bumped; the change notes this so a future Docsy upgrade re-syncs the override.

## Verification
- **Local only (no e2e)**: `hugo serve`; type a query → client-side results; the network panel shows
  `/js/lunr-2.3.9.min.js` and `/js/jquery-3.7.1.min.js` from the origin, with no `unpkg`/`code.jquery.com`
  request. The `.NET` gate is untouched.

## Risks
- The Lunr index grows with the doc set; acceptable at the current size. If it becomes large, Algolia (already
  wired) is the escape hatch.
