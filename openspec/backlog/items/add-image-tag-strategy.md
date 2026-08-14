---
id: add-image-tag-strategy
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
nginx-proxy: n/a (DockYarp packaging/ops)
provenance: 2026-08-14 user backlog discussion (Docker Hub tag scheme)
status: backlog
---

## Why
Today the image publish pushes only `:{version}` + `:latest` (`image.yml` → Nuke `DockerPublish`). That is too coarse:
`:latest` moves for **any** `v*` tag (including prereleases), there are no rolling major/minor tags to pin against, and
there is no tag for the latest in-development build. Users need SemVer-style rolling tags and a bleeding-edge channel.

## Current state
- `image.yml` triggers on `push tags: v*` (+ `workflow_dispatch`); `version = ${ref#v}`; Nuke `DockerPublish` pushes
  `:{version}` + `:latest` to the resolved registry. Versions come from **GitVersion (GitFlow)** — `develop` resolves
  a prerelease (`0.1.0-alpha.N`), a `v*` tag resolves the release version.

## Proposed change (sketch) — agreed scheme (2026-08-14)
Derive the tag set from the SemVer version (GitVersion) in the **single Nuke path** ([[nuke-single-build-path]]); the
workflow only triggers.
- **Stable release tag `vX.Y.Z`** → push `X.Y.Z` (immutable, specific) **+** `X.Y` (rolling latest patch of the minor)
  **+** `X` (rolling latest of the major) **+** `latest` (last stable). *(Docker convention drops the `v` prefix on the
  image tag, like `node:20`.)*
- **Prerelease tag `vX.Y.Z-<pre>`** (alpha/beta/rc) → push `X.Y.Z-<pre>` **only** — never move `X.Y`/`X`/`latest`.
- **In-development** (push to `develop`, no tag) → push a **bleeding-edge** tag: recommended **`edge`** (Docker-idiomatic;
  alternatives `nightly`/`canary`/`dev` — pick one) plus the GitVersion prerelease version (`0.1.0-alpha.N`). Needs a
  `develop`-push trigger added to `image.yml` (evening-push constraint applies).

So: `latest` = last stable · `X` = last of that major · `X.Y` = last patch of that minor · `X.Y.Z` = exact immutable ·
`edge` = latest develop build.

## Acceptance criteria (→ scenarios)
- **WHEN** a stable `vX.Y.Z` is released **THEN** the registry has `X.Y.Z`, `X.Y`, `X`, and `latest` all pointing at it.
- **WHEN** a prerelease `vX.Y.Z-rc.1` is tagged **THEN** only `X.Y.Z-rc.1` is pushed (`latest`/`X`/`X.Y` unchanged).
- **WHEN** `develop` is pushed **THEN** the `edge` tag (and the prerelease version) is updated to that build.

## Notes / risks / references
- Guard `:latest` (and the rolling tags) against prereleases — the current unconditional `:latest` is the main bug.
- Immutable `X.Y.Z` must never be overwritten; the base-image refresh (`add-base-image-rebuild`) already rebuilds only
  `:latest`, so reconcile which rolling tags a base-image refresh should move.
- Depends on the release flow producing the `v*` tags → pairs with [`add-release-changelog`](add-release-changelog.md)
  (release-please). The "Supported tags" list feeds [`add-registry-readme-sync`](add-registry-readme-sync.md).
- Refs: `.github/workflows/image.yml`, `build/Build.cs` (`DockerPublish` tag logic), `GitVersion.yml`.
