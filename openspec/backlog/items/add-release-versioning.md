---
id: add-release-versioning
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal — release engineering)
provenance: 2026-08-06 CI/ops backlog expansion
---

## Why
There is no versioning or release automation: `build/Build.cs` carries `Version = ""`, assemblies are unstamped,
and there is no changelog or GitHub Release flow. Image tags (`add-ci-image-publish`) need a real version, and
users need release notes.

## Current state
- No version source; no tags-driven versioning; no changelog; no GitHub Release step.

## Proposed change (sketch)
- Adopt **Nerdbank.GitVersioning** (SDK-friendly, CPM-compatible, `version.json`) to derive the version from git
  height + tags and stamp all assemblies (`AssemblyVersion`/`InformationalVersion`), and expose it to the Nuke
  build (`Version`) and the image tag.
- On a `v*` tag: a release workflow creates a **GitHub Release** with an auto-generated **changelog** (from
  Conventional Commits — e.g. `git-cliff` or `release-please`; decide in design) and triggers `add-ci-image-publish`.

## Acceptance criteria (→ scenarios)
- **WHEN** the build runs **THEN** assemblies + the admin API version reflect the git-derived version.
- **WHEN** a `vX.Y.Z` tag is pushed **THEN** a GitHub Release with a changelog is created and the image is tagged
  `X.Y.Z`.

## Notes / risks / references
- Commit convention: the repo already uses gitmoji (`:sparkles: feat:` …) — reconcile with the changelog tool's
  Conventional-Commits expectations (a gitmoji-aware config, or a mapping) in the design.
- Depends on `add-ci-build-test`; feeds `add-ci-image-publish`. Sibling: `add-doc-versioning` (doc-site).
