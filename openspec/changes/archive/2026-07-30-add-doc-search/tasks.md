## 1. Self-host the search libraries (AG-DOC)
- [x] 1.1 Download `lunr@2.3.9` and `jquery@3.7.1` minified into `docs-site/static/js/`, verified against
      Docsy's SRI hashes (byte-identical — both MATCH)

## 2. Enable offline search (AG-DOC)
- [x] 2.1 `hugo.toml`: `params.offlineSearch = true` (+ summary length / max results); document the Algolia
      alternative as commented `params.search.algolia`
- [x] 2.2 Project override `layouts/_partials/head.html` (faithful copy of Docsy's) with the jQuery and Lunr
      `<script src>` pointed at the local `/js/` copies via `relURL`

## 3. Verify (AG-DOC)
- [ ] 3.1 Local `hugo serve`: the search box returns client-side results; Lunr/jQuery load from `/js/` with no
      unpkg / code.jquery.com request — user to confirm in the local preview
- [x] 3.2 .NET `Nuke Test` gate unaffected (no code touched)
