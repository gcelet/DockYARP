---
id: add-release-changelog
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal — release engineering)
provenance: 2026-08-06 split out of add-release-versioning (repo-dependent half); release-workflow question added 2026-08-06
---

## Why
Once the version is git-derived (`add-release-versioning`), cutting a new version should be **as automatic as
possible** and create the **GitHub Release + changelog** on its own, then trigger the image publish. This half is
**repo-dependent** (needs the GitHub repo + real tags to run) and requires two decisions — the **git release
workflow** (how a version is cut) and the **changelog tool** — so it was split out of `add-release-versioning`.

## Current state
- `add-release-versioning` stamps assemblies + the image + `/api/version` from the GitVersion version. No GitHub
  Release, no changelog. `image.yml` already publishes the image on a `v*` tag.

## Release workflow — DECISION TO MAKE (git flow for cutting a version)
The user wants version creation to be **as automatic as possible**, incl. the GitHub Release, but is unsure whether
to go through a release branch, a merge to `main`, or something else. Options (repo is single-trunk / GitHubFlow):

1. **release-please on `main` (recommended — most automatic, no release branch):** a workflow runs on push to
   `main` and maintains a cumulative "release PR"; merging that PR **tags `vX.Y.Z` and creates the GitHub Release**
   automatically. Viable here because commits carry the Conventional `type:` *after* the gitmoji
   (`:sparkles: feat:` …) — configure release-please to ignore the leading emoji. Reconcile the version source with
   `add-release-versioning` (GitVersion) — either let release-please own the version, or feed GitVersion.
2. **Tag-on-`main` + git-cliff:** push a `vX.Y.Z` tag on `main` → a workflow builds the changelog (git-cliff, easy
   to configure for gitmoji) and creates the Release. Less automatic (you choose the tag), but GitVersion already
   computes the version so the tag could be automated too.
3. **Release branch / GitFlow:** rejected as overkill for single-trunk (we chose GitHubFlow in `GitVersion.yml`).

## Proposed change (sketch)
- Implement the chosen workflow above; on release, create the **GitHub Release** (attaching the changelog) and let
  `image.yml` publish the tagged image.
- **Changelog tool:** gitmoji-aware — `git-cliff` (regex mapping gitmoji+type → sections) or `release-please`
  (bundled with option 1). Decide alongside the workflow.

## Acceptance criteria (→ scenarios)
- **WHEN** a version is cut per the chosen workflow **THEN** a GitHub Release is created with a changelog generated
  from the commits since the previous release, grouped by the repo's gitmoji commit convention.
- **WHEN** the release is created **THEN** the tagged image publish (`image.yml`) runs for that version.

## Notes / risks / references
- **Blocked on the repo** (`gcelet/DockYARP` not created yet): author + `actionlint` locally, real run deferred.
- **CI GitVersion needs a full checkout (`fetch-depth: 0`)** — shallow clones break the height/tag calculation.
- Depends on `add-release-versioning` (version source) and `add-ci-image-publish` (publish). Sibling:
  `add-doc-versioning` (doc-site).
