## Why
DockYARP's documentation lives only as scattered Markdown (OpenSpec, architecture notes, module READMEs). There
is no structured, navigable documentation site. This is the keystone of the doc-site family: a Hugo + Docsy
site under `docs-site/`, themed with the DockYARP identity, that every other doc-site item builds on. A
user-authored design draft already exists and is adopted as the base.

## What Changes
- Add a `docs-site/` Hugo + Docsy project (Hugo **Extended** required), isolated from the .NET build.
- Apply the DockYARP brand (SCSS overrides, no theme fork) with **first-class light and dark themes**.
- Define the information architecture / nav: Getting Started, Configuration (labels + app config),
  Architecture, Deployment, Contributing.
- Seed honest starter content using the **real** labels (`VIRTUAL_HOST`, `VIRTUAL_PORT`, `LETSENCRYPT_HOST`,
  `DOCKYARP_*`) — the draft's placeholder content is rewritten.
- Configure for the target repo `github.com/gcelet/DockYARP` and a swappable `baseURL` (GitHub Pages by
  default; `dockyarp.com` later). No admin-portal page.

## Capabilities
### New Capabilities
- `documentation`: DockYARP ships a Hugo + Docsy documentation site scaffolded under `docs-site/`.
### Modified Capabilities
<!-- None. -->

## Impact
- **Files**: new `docs-site/` (Hugo config, brand SCSS incl. dark mode, partials, favicons/images, content
  sections, `package.json`, `.gitignore`, `README`). No `src/` or `tests/` change; the .NET `Nuke Test` gate is
  unaffected.
- **Verification (deferred — tooling not installed)**: `hugo` is not installed on this machine and Docsy needs
  Hugo **Extended** + Go (modules) + npm (via fnm). So *that the site builds* (`hugo mod get` + `npm install` +
  `hugo` build) cannot be verified here; it is deferred to a local/CI run once the toolchain is installed —
  analogous to an e2e run. The scaffold, config, and content are the deliverable.
- **Follow-ups**: `add-doc-brand-theme` (largely pre-delivered here), `add-doc-search`, `add-doc-versioning`,
  `add-doc-ci-publish`. Deep per-feature content is iterative.
- **Owning agent**: AG-DOC. Resolves `add-doc-site-foundation`.
