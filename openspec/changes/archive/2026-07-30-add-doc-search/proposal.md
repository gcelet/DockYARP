## Why
The documentation site ships navigation only; content is not searchable. Search lets users jump to the right
page as the doc set grows. Docsy has built-in **offline (Lunr.js) search**, but it loads the Lunr library from
a CDN (unpkg) — inconsistent with the site's no-CDN direction (fonts are already self-hosted) and it breaks
offline / leaks requests.

## What Changes
- Enable Docsy's **offline search** (`params.offlineSearch`): the search index is generated at build time and
  queries run entirely client-side (no search service).
- **Self-host the Lunr library** (and jQuery, which Docsy loads from a CDN in the same head): override
  `head.html` to serve both from the site's own `/js/`, so search — and the head — make **no CDN request**.
- Keep **Algolia DocSearch** available as an opt-in alternative via `params.search.algolia` (Docsy's built-in
  path is preserved by the override); the default stays Lunr so the site is never dependent on a third party.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the docs site provides client-side offline search with a self-hosted Lunr library (no CDN);
  Algolia remains an optional alternative.

## Impact
- **Files**: `docs-site/static/js/lunr-2.3.9.min.js` + `jquery-3.7.1.min.js`; a project override
  `layouts/_partials/head.html` (Docsy's, with the two CDN scripts pointed at the local copies); `hugo.toml`
  (`offlineSearch = true`, Algolia example commented).
- **Verification (local, no E2E)**: `hugo serve` → the search box returns client-side results and the network
  panel shows Lunr/jQuery served from `/js/` (no unpkg / code.jquery.com request). User runs the local preview.
- **Note**: overriding `head.html` pins it to the Docsy v0.16 head; revisit if the Docsy submodule is bumped.
- **Owning agent**: AG-DOC. Resolves `add-doc-search`.
