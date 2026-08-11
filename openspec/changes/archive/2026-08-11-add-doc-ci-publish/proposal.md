## Why
The documentation site (`docs-site/`, Hugo + Docsy) is only valuable once it is **published and kept current**.
Today it builds only via a local, ambient Hugo install and nothing publishes it. Now that the GitHub repo exists,
we can stand up a reproducible build + GitHub Pages publish — and do a **test publish before v1** to de-risk the
launch (Pages/Docsy/submodule surprises surface now, not at the release).

## What Changes
- **Reproducible build (no ambient Hugo)**: pin **`hugo-extended`** in `docs-site/package.json` (devDependency) so
  the exact Hugo Extended version is installed by npm, and add a Nuke **`Docs`** target that runs `npm ci` (installs
  Hugo + PostCSS and initializes the Docsy submodule via the `prepare` script) then `hugo --minify --baseURL …`,
  producing `docs-site/public/`. The single build path stays in Nuke.
- **CI publish to GitHub Pages** (`.github/workflows/docs.yml`): a **build check on every PR**, and a **build +
  deploy on push to `develop`** (plus `workflow_dispatch`), via the Pages artifact + `deploy-pages` (OIDC).
- **Publishes from `develop` pre-1.0** on purpose — `main` stays empty until the first release, so a `push:main`
  deploy would never publish before v1. (Extend to `main` at v1.)

## Capabilities
### Modified Capabilities
- `documentation`: the site is built reproducibly and published to GitHub Pages via CI.

## Impact
- **Code**: `docs-site/package.json` (pin `hugo-extended`), `build/Build.cs` (new `Docs` target),
  `.github/workflows/docs.yml` (new). No application code.
- **Manual (user, one-time)**: repo → Settings → Pages → **Source = GitHub Actions**. Then a push to `develop`
  performs the **test publish**.
- **Validation**: `./build.ps1 Docs` builds `public/` locally tonight; the live Pages deploy validates on push.
- **Owning agent**: AG-DEP/AG-DOC. Docsy stays a git submodule → CI checkout uses `submodules: recursive`.
