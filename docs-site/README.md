# DockYARP documentation site

Hugo + [Docsy](https://www.docsy.dev/), themed with the DockYARP identity. Lives under `docs-site/`, isolated
from the .NET solution.

## Requirements

- **Node.js** — managed by **fnm** on this machine (not active by default: activate fnm first). Node is the only
  toolchain dependency: **Hugo Extended is pinned as an npm devDependency** (`hugo-extended`), so no ambient Hugo
  install is needed; PostCSS/autoprefixer also come from npm.
- **No Go / no Hugo Modules.** Docsy is a **Git submodule**, initialized automatically by `npm install` (the
  `prepare` script) — no Go toolchain.

## Install

```bash
cd docs-site
npm install     # pinned Hugo Extended + PostCSS, and initializes the Docsy submodule (prepare script)
```

## Run

```bash
# One-time (or after a clean checkout): vendor Docsy's SCSS deps (Bootstrap, Font Awesome) into the theme.
npm install --prefix themes/docsy/theme --omit=dev --omit=peer   # requires Node >= 24 (Docsy v0.16)
npm run serve     # http://localhost:1313/  (Hugo from node_modules/.bin)
npm run build     # production build into public/
```

**Reproducible build / CI:** from the repo root, `./build.ps1 Docs` (or `./build.sh Docs`) runs the Nuke **`Docs`**
target — `npm ci`, then vendors Docsy's theme SCSS deps (the command above), then Hugo → `docs-site/public`. So a
one-shot `nuke Docs` also prepares a clean checkout for `npm run serve`. The `Docs` workflow
(`.github/workflows/docs.yml`) builds on every PR and publishes to **GitHub Pages** on push to `develop`. One-time
repo setup: **Settings → Pages → Source = GitHub Actions**.

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
