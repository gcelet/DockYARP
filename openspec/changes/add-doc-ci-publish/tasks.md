## 1. Reproducible build (AG-DEP)
- [x] 1.1 `docs-site/package.json`: add `hugo-extended` (pinned `^0.164.0`) to devDependencies; lockfile updated. Also
      dropped the fragile `_prepare:docsy` theme postinstall from `prepare` (Node-24 + nested-npm-PATH failure; the
      build doesn't need the theme's npm deps — Hugo resolves the submodule directly)
- [x] 1.2 Nuke `Docs` target: `npm ci` in `docs-site/` then `npx hugo --minify --baseURL {DocsBaseUrl}` → `docs-site/public/`
- [x] 1.3 `[Parameter] DocsBaseUrl` default `https://gcelet.github.io/DockYARP/` (project subpath); overridable
- [x] 1.4 Local verify: `./build.ps1 Docs` → complete `docs-site/public/` (index + 14 pages + css/fonts/search), exit 0

## 2. CI publish (AG-DEP)
- [x] 2.1 `.github/workflows/docs.yml`: `pull_request` + `push: [develop]` + `workflow_dispatch`, path-filter `docs-site/**`
- [x] 2.2 permissions `pages: write` + `id-token: write` + `contents: read`; `concurrency: pages`
- [x] 2.3 `build` job: checkout (`submodules: recursive`, `fetch-depth: 0`) + setup-dotnet + setup-node (24) + `./build.sh Docs` + `upload-pages-artifact` (`docs-site/public`)
- [x] 2.4 `deploy` job (`needs: build`, not on PR): `actions/deploy-pages`
- [x] 2.5 YAML validated. `actionlint` on a capable machine + the real deploy (after **Settings → Pages → Source = GitHub Actions**) run on the repo

## 3. Docs (AG-DOC)
- [x] 3.1 `docs-site/README.md`: pinned-`hugo-extended` build, the `nuke Docs` target, the Pages workflow + the manual Pages-source step

## 4. Verify (AG-DEP)
- [x] 4.1 Nuke `Docs` green locally; app build/tests untouched (docs build isolated from `DockYarp.slnx`)
