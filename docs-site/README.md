# DockYARP documentation site

Hugo + [Docsy](https://www.docsy.dev/), themed with the DockYARP identity. Lives under `docs-site/`, isolated
from the .NET solution.

## Requirements

- **Hugo Extended ≥ 0.160.1** — the *extended* build (SCSS/PostCSS) is required by Docsy.
  `winget install Hugo.Hugo.Extended` — verify with `hugo version` (must contain `extended`).
- **Node.js LTS** — managed by **fnm** on this machine (not active by default): activate fnm, then
  `fnm install --lts` / `fnm default lts-latest`. Used by Docsy/PostCSS to build the CSS.
- **No Go / no Hugo Modules.** Docsy is installed as a **Git submodule** (see below), so the Go toolchain is
  not needed on Windows.

## Install (Docsy via Git submodule — no Go)

```bash
cd docs-site
git submodule add https://github.com/google/docsy.git themes/docsy
(cd themes/docsy && git checkout v0.16.0)
(cd themes/docsy && npm run postinstall)   # theme runtime deps — NOT `npm install`
npm install                                # project PostCSS/autoprefixer
```

After the first clone of the repo, `npm install` at `docs-site/` runs the `prepare` script, which initializes
the submodule and its dependencies automatically.

## Run

```bash
hugo serve        # http://localhost:1313/
hugo --minify     # production build into public/
```

> The site config sets `theme = ["docsy/theme"]` (the theme lives under `themes/docsy/theme` in Docsy's
> monorepo layout).

## Layout

| Path | Purpose |
|------|---------|
| `hugo.toml` | Site config, menus, brand params, `github_subdir = docs-site` |
| `assets/scss/_variables_project.scss` | Brand palette + typography (loaded **before** Bootstrap) |
| `assets/scss/_styles_project.scss` | Custom component styles + light/dark overrides (loaded **after** Bootstrap) |
| `layouts/partials/navbar-logo.html` | Brand lockup (mark + DockYARP wordmark) |
| `layouts/partials/favicons.html` | Favicon set |
| `static/favicons/`, `static/images/` | Favicons, social banner, logo assets |
| `content/en/` | Landing page + documentation sections |

## Configuration to finalize

- **Domain / `baseURL`**: defaults to the GitHub Pages project URL `https://gcelet.github.io/DockYARP/` (note
  the `/DockYARP/` subpath). Swap to `https://dockyarp.com/` once the domain is reserved, or override per build
  with `hugo --baseURL`.
- **Docker Hub link** (`hugo.toml` → `params.links.user`): confirm the published image namespace (the Nuke
  build tags the image `dockyarp`).
- **Fonts**: currently Google Fonts (Space Grotesk + JetBrains Mono); consider self-hosting for GitHub Pages /
  offline (tracked with `add-doc-brand-theme`).

## Brand tokens

| Token | Hex | Use |
|-------|-----|-----|
| Ink | `#0F1226` | navbar, cover, code blocks |
| Blue | `#3B82F6` | hexagon / structure, info |
| Violet | `#7C3AED` | primary action, links, active nav |
| Teal | `#14B8A6` | flow, success |
| Mist | `#E2E8F0` | borders, rules |
