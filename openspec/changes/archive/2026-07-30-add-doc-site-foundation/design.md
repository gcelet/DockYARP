# Design — add-doc-site-foundation

## Context
A user-authored Hugo + Docsy draft (`…/perso/DockerYARP/hugo-site`) already carries the DockYARP brand SCSS,
favicons, logo partial, and a Docsy scaffold. It is adopted as the base and relocated into the repo under
`docs-site/`, then corrected. `hugo` is not installed here, so the build is not run in this change.

## Decisions

### 1. Location and isolation
The site lives in `docs-site/` at the repo root, fully isolated from `DockYarp.slnx` and the Nuke build. A
`docs-site/.gitignore` excludes Hugo build output (`public/`, `resources/`, `.hugo_build.lock`) and
`node_modules/`.

### 2. Adopt the draft, fix correctness
Carry over the draft, then fix what would break or mislead:
- **Real labels** in content: `VIRTUAL_HOST`, `VIRTUAL_PORT`, `VIRTUAL_PATH`, `VIRTUAL_PROTO`,
  `LETSENCRYPT_HOST`/`_EMAIL`, `DOCKYARP_*` (nginx-proxy compatible). The draft's `dockyarp.host`/`.port`/`.tls`
  are wrong and rewritten.
- **Repo/URLs**: `github_repo = https://github.com/gcelet/DockYARP`, `github_subdir = "docs-site"` (so "edit
  this page" links resolve), `github_branch = main`. `baseURL` defaults to the GitHub Pages project URL
  (`https://gcelet.github.io/DockYARP/`, with the `/DockYARP/` subpath) and is swappable to `dockyarp.com`.
- **Menu**: drop the duplicate `[[menu.main]]` "Docs" entry in `hugo.toml`; let section front matter drive nav.
- **`disableKinds`**: `["taxonomy", "term"]` (modern Hugo kind names).
- **Remove** the admin-portal feature/link from the landing (out of scope for the site).

### 3. First-class light and dark themes
Dark mode is a hard requirement (the user always uses dark). Docsy's light/dark support is enabled and the
brand SCSS provides explicit dark overrides (surfaces, content background, sidebar, code, borders) verified for
WCAG AA contrast — not a light-only design with a token toggle.

### 4. Information architecture
Product-facing sections seeded with honest, minimal-but-true content:
`Getting Started` · `Configuration` (container labels + app configuration) · `Architecture` · `Deployment` ·
`Contributing` (spec-driven/OpenSpec workflow). Deep per-capability reference content is iterative (later work).

### 5. Fonts
The draft uses Google Fonts (Space Grotesk + JetBrains Mono). For GitHub Pages / offline and privacy,
self-hosting is preferred; this is noted for `add-doc-brand-theme` / CI and not blocked here.

## Verification
- **Not runnable here**: `hugo` (Extended) + Go + npm (fnm) are not installed, so the site build is not
  executed. The `.NET` `Nuke Test` gate stays green (no code change). Building/serving the site is a documented
  local/CI step deferred until the toolchain is present.

## Risks
- Docsy module wiring (`hugo mod get github.com/google/docsy@…`) and Hugo Extended version compatibility can
  only be confirmed by an actual build; the README pins the commands and version to try first.
