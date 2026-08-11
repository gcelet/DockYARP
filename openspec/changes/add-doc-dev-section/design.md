# Design — add-doc-dev-section

## Enrich, don't duplicate
The authoritative developer docs are in the repo (`docs/testing.md` = test pyramid + e2e coverage map,
`docs/architecture.md`, `AGENTS.md`). The site's Contributing page stays the **entry point** and **links** them, so
there is a single source of truth and no drift. Only a short summary lives on the site.

## Content added to `contributing.md`
A **Testing** section after "Build & test":
- The pyramid in one paragraph: **unit** (per `*.Tests` project, the default), **integration**
  (`DockYarp.IntegrationTests`, the ASP.NET pipeline, no Docker), **end-to-end** (Aspire + Docker, only what needs
  the real stack). Link the full **coverage map** → `docs/testing.md`.
- The Nuke targets: `./build.ps1 Test` (unit + integration gate), `E2E` (Docker), `Docs` (this site).

A one-line **Architecture** pointer → `docs/architecture.md`.

## Repo links follow the centralized branch
All links to in-repo files use the `repo-file` shortcode (`{{< repo-file "docs/testing.md" >}}`), which builds the
URL from `params.github_repo` + `params.github_branch`. Changing the target branch (e.g. `develop` → `main` at v1)
is a single setting; these links move with it. No hardcoded `blob/<branch>/…`.

## Out of scope
- Rewriting the whole page / a full "Developing" restructure — keep it a focused Testing + pointers addition.
- Publishing the repo docs (`docs/*.md`) on the site — they stay repo-internal and are linked, not mirrored.
