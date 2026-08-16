## 1. New Releasing page (AG-DOC)

- [x] 1.1 Create `docs-site/content/en/docs/releasing.md` with front matter `title: Releasing`, `weight: 8`,
      a one-line `description`, matching the style of the other reference pages.
- [x] 1.2 Write the "First release only" section: creating `main` from `develop` and tagging `v0.1.0`.
- [x] 1.3 Write the "Every release" section: checking the GitVersion-computed version, then the exact
      `git tag`/`git push` commands.
- [x] 1.4 Write the "What happens automatically" section, linking to `.github/workflows/release.yml` and
      `.github/workflows/image.yml` via the `{{< repo-file >}}` shortcode rather than restating their steps.
- [x] 1.5 Write the worked example, clearly labeled as illustrative (placeholder version, no real release cut
      yet). Caught and fixed an inaccuracy while writing it: the example originally implied the rolling-tag
      scheme (`X.Y`/`X`/`latest`) from the still-open `add-image-tag-strategy` backlog item; corrected to what
      `image.yml` actually publishes today (`:{version}` + `:latest` only).

## 2. Trim the Contributing page (AG-DOC)

- [x] 2.1 Replace `contributing.md`'s "Releases" section body with a short pointer to the new Releasing page
      (`{{< relref "releasing.md" >}}`), keeping the section heading.

## 3. Validation (AG-DEP / AG-DOC)

- [x] 3.1 Built the docs site locally (`./build.ps1 Docs`) — succeeded, 15 pages (was 14). Verified in the
      built output: `docs-site/public/docs/releasing/index.html` exists, and `contributing.md`'s
      `{{< relref "releasing.md" >}}` resolved to `/DockYARP/docs/releasing/` with no Hugo warning; sidebar
      nav shows Releasing ordered right after Contributing (weight 8 vs 7), as intended.
- [x] 3.2 Run `npx @fission-ai/openspec@latest validate add-doc-release-guide --strict`.
