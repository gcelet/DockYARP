## Why
The published site's `contributing.md` covers the OpenSpec change lifecycle + basic build/test, but the deeper
**"Developing on DockYARP"** material is thin. In particular, the **test-pyramid strategy** and the **e2e coverage
map** now live in `docs/testing.md` (repo-internal) and are **not visible on the site**. A contributor reading the
site should find the test strategy, the build/test/e2e commands, and pointers into the codebase.

## What Changes
- Enrich `docs-site/content/en/docs/contributing.md` with a **Testing** section: the pyramid (unit / integration /
  e2e, when each applies) and a link to the repository's `docs/testing.md` (the coverage map), plus the Nuke targets
  (`Test`, `E2E`, `Docs`) and a pointer to `docs/architecture.md`.
- **Link** the authoritative in-repo docs rather than duplicating them (site = entry point, repo docs = source of
  truth, no drift). Repo links use the `repo-file` shortcode so they follow the **centralized target branch**
  (`params.github_branch`) — one knob, no hardcoded branch.

## Capabilities
### Modified Capabilities
- `documentation`: the Contributing page provides developer guidance (test strategy + repo-doc pointers).

## Impact
- **Code**: `docs-site/content/en/docs/contributing.md` (content). Uses the existing `repo-file` shortcode.
- **Validation**: the site builds (`./build.ps1 Docs` / `hugo`); links resolve to the configured branch.
- **Owning agent**: AG-DOC. Pairs with `add-doc-ci-publish` (so the enriched page is published) and `docs/testing.md`
  (which it surfaces).
