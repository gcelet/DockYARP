# Design — add-doc-ci-publish

## Reproducible build via npm-pinned Hugo (no ambient toolchain)
The site already needs **npm** (Docsy's PostCSS/autoprefixer) and Hugo **Extended** (SCSS). Instead of an ambient
`winget install Hugo` (unpinned, per-machine), pin **`hugo-extended`** as a `docs-site` devDependency — that npm
package ships the exact Extended binary. Then the whole toolchain is reproducible from `package.json` + the lockfile:

- Nuke **`Docs`** target: `npm ci` in `docs-site/` (installs `hugo-extended` + PostCSS, and the `prepare`/`postinstall`
  scripts initialize the Docsy submodule deps), then `npx hugo --minify --baseURL {DocsBaseUrl}` → `docs-site/public/`.
- `[Parameter] DocsBaseUrl` defaults to the project Pages URL `https://gcelet.github.io/DockYARP/` (note the
  `/DockYARP/` subpath — a **project** site, not a user/org site). Overridable for other hosts / the future domain.
- Keeps the build in Nuke (single build path); CI just calls `./build.sh Docs`.

## GitHub Pages workflow (`.github/workflows/docs.yml`)
- **Triggers**: `pull_request` (build check, no deploy) + `push` to `develop` + `workflow_dispatch`. Path-filter to
  `docs-site/**` (and the workflow itself) so app-only changes don't rebuild docs.
- **Why `develop`, not `main`**: `main` is empty until v1, so publishing on `main` would never fire pre-1.0. Publish
  from `develop` now; add `main` at the first release.
- **Permissions**: `pages: write`, `id-token: write`, `contents: read`. `concurrency: pages` (cancel-in-progress
  false) so deploys serialize.
- **Jobs**:
  - `build`: checkout (`submodules: recursive`, `fetch-depth: 0` for `enableGitInfo`), `actions/setup-node`,
    `./build.sh Docs`, then `actions/upload-pages-artifact` with `path: docs-site/public`.
  - `deploy` (`needs: build`, **not** on `pull_request`): `actions/deploy-pages`.
- Deploy is workflow-native (Pages OIDC artifact) — no gh-pages branch, so **no evening-hours concern** (no commits
  are created by the deploy).

## One-time manual setup (user)
Repo → **Settings → Pages → Source = GitHub Actions**. Until then the deploy job has no target. After it, a push to
`develop` performs the **test publish** — the live validation of the whole pipeline before v1.

## Out of scope
- Doc **versioning** (multiple published versions) → `add-doc-versioning`.
- Switching the theme install from a submodule to Hugo Modules (a preference, not needed) — kept as a submodule.
- The custom domain (`dockyarp.com`) — `baseURL` stays the Pages URL; swap later.
