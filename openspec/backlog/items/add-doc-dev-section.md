---
id: add-doc-dev-section
capability: documentation
agent: AG-DOC
tier: C-doc
priority: low
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user reflection 2026-08-10 — "Developing on DockYARP" section + surface the test strategy on the site
---

## Why
The published site's `contributing.md` covers the OpenSpec lifecycle + basic build/test, but the deeper
**"Developing on DockYARP"** material is thin or repo-only. In particular, the test-pyramid strategy and the e2e
coverage map now live in `docs/testing.md` (repo-internal) and are **not visible on the site**. Contributors
reading the site should find how to build, test, and navigate the codebase.

## DockYarp today
- Site page `docs-site/content/en/docs/contributing.md` = OpenSpec loop + `dotnet build/test` + a pointer to the
  docs-site README. No testing strategy, no link to `docs/testing.md`, no architecture/Nuke-target orientation.
- `docs/testing.md` (test pyramid + e2e coverage map) and `docs/architecture.md` are authoritative but repo-only.

## Proposed change (sketch)
- Enrich `contributing.md` (or add a **"Developing"** section) with: the **test pyramid** (unit / integration / e2e,
  when to use each) linking `docs/testing.md`; the Nuke targets (`Test`, `E2E`, `Docs`); an architecture orientation
  linking `docs/architecture.md`; and the multi-machine / evening-hours git workflow at a high level.
- Prefer **linking** the authoritative repo docs (`docs/testing.md`, `docs/architecture.md`, `AGENTS.md`) over
  duplicating them, to avoid drift — keep the site as the entry point, the repo docs as the source of truth.

## Acceptance criteria (→ scenarios / done state)
- **WHEN** a contributor opens the site's Contributing/Developing section **THEN** they find the test strategy
  (with a link to `docs/testing.md`) and the build/test/e2e commands.
- The site does not duplicate repo-doc content that would drift; it links it.

## Notes / risks / references
- Small doc task. Pairs with `add-doc-ci-publish` (so the enriched page is actually published). `docs/testing.md`
  already carries the coverage map; this item is about **surfacing** it on the site.
