---
id: add-doc-release-guide
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal initiative — release process documentation, no parity row)
provenance: 2026-08-16 user request, immediately following add-release-changelog
---

## Why
`add-release-changelog` wired the *output* half of a release (tag push → git-cliff changelog → GitHub Release),
but nothing walks a human through the *input* half: when `main` gets created, how the version is decided, and
what command actually cuts a release. The user wants cutting a release to be **as simple and automatic as
possible** — right now that knowledge only lives scattered across `GitVersion.yml`, the short "Releases" note
just added to `contributing.md`, and this backlog's history. Needs a clear, precise, findable doc-site page.

## nginx-proxy behavior
N/A — internal initiative (DockYarp's own release process, not a proxy feature). No `parity.md` row.

## DockYarp today
- `docs-site/content/en/docs/contributing.md` has a short "Releases" section (added with `add-release-changelog`)
  stating the mechanics in 3 sentences: manual GitVersion-informed tag push → `release.yml` (git-cliff changelog
  + GitHub Release) → `image.yml` (tagged image publish). No step-by-step walkthrough, no worked example.
- Under GitFlow ([[github-repo-init-plan]]), `main` does not exist yet — it is created only at the *first*
  release by merging `develop` in and tagging `v0.1.0`. That one-time bootstrap step isn't documented anywhere
  either, and is easy to get wrong the one time it matters.
- GitVersion (`GitVersion.yml`, GitFlow workflow) already computes the version from commit height/branch —
  nothing forces a contributor to manually pick a SemVer number, but nothing tells them how to *read* the
  computed version before tagging, either.

## Proposed change (sketch)
A dedicated release-process page (or a fuller expansion of `contributing.md`'s "Releases" section if a whole
new page is overkill — decide at propose-time) covering, in order:
- **First release only**: how/when to create `main` (merge `develop` → `main`, tag `v0.1.0`).
- **Every release**: how to read the version GitVersion would compute (`dotnet gitversion` / Nuke output) before
  tagging, the exact tag command, and what happens automatically after (image publish, changelog, GitHub
  Release) — link rather than duplicate `release.yml`'s behavior.
- A worked example (one full release, start to tag push, showing the resulting GitHub Release).

## Acceptance criteria (→ scenarios)
- **WHEN** a contributor wants to cut a release **THEN** the doc site has one page that tells them exactly what
  to run, in order, with no need to read `GitVersion.yml` or the workflow YAML directly.
- **WHEN** it is the *first* release **THEN** the doc explicitly covers the `main`-creation bootstrap step (not
  assumed knowledge).
- **WHEN** the release process changes (e.g. tag-cutting gets automated later) **THEN** this page is the single
  place that needs updating — no duplicated release instructions elsewhere.

## Notes / risks / references
- **Possible companion item, not this one**: "as simple and automatic as possible" may eventually want a small
  tooling piece too (e.g. a Nuke target that computes the next version and pushes the tag in one command,
  reducing the manual step `add-release-changelog`'s design.md deliberately left as a Non-Goal). Don't conflate
  scope here — this item is documentation of the *current* manual process; a `add-release-tag-automation`-style
  item would be the place to actually reduce the manual step, if/when wanted.
- Depends on `add-release-changelog` (done) for what to document. Sibling: `add-doc-versioning` (docs-site
  versioning, a different concern — that's about serving old doc versions, not the release process itself).
- Refs: `GitVersion.yml`, `.github/workflows/release.yml`, `docs-site/content/en/docs/contributing.md`.
