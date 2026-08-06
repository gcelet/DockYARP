---
id: add-release-changelog
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal — release engineering)
provenance: 2026-08-06 split out of add-release-versioning (repo-dependent half)
---

## Why
Once the version is git-derived (`add-release-versioning`), a `v*` tag should produce a **GitHub Release with an
auto-generated changelog** and trigger the image publish. This half is **repo-dependent** (needs the GitHub repo +
real tags to run) and requires a **changelog-tool decision**, so it was split out of `add-release-versioning`.

## Current state
- `add-release-versioning` stamps assemblies + the image + `/api/version` from the NB.GV version. No GitHub
  Release, no changelog. `image.yml` already publishes the image on a `v*` tag.

## Proposed change (sketch)
- A release workflow on a `v*` tag: build the changelog from the commit history and create a **GitHub Release**
  (attaching the changelog), then let `image.yml` publish the tagged image.
- **Changelog-tool decision (design):** the repo uses **gitmoji** commit prefixes (`:sparkles: feat:` …), not
  plain Conventional Commits. Pick a gitmoji-aware path — e.g. `git-cliff` with a custom regex config that maps
  the gitmoji+type prefixes to sections, or `release-please` with a matching config — reconciled with the repo's
  actual convention. Decide with the user.

## Acceptance criteria (→ scenarios)
- **WHEN** a `vX.Y.Z` tag is pushed **THEN** a GitHub Release is created with a changelog generated from the
  commits since the previous tag, grouped by the repo's gitmoji commit convention.
- **WHEN** the release is created **THEN** the tagged image publish (`image.yml`) runs for that version.

## Notes / risks / references
- **Blocked on the repo** (`gcelet/DockYARP` not created yet): author + `actionlint` locally, real run deferred.
- Depends on `add-release-versioning` (version source) and `add-ci-image-publish` (publish). Sibling:
  `add-doc-versioning` (doc-site).
