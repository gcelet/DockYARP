## Why

`image.yml` publishes exactly `:{version}` + an **unconditional** `:latest` for every `v*` tag — including
prereleases. That means a `v0.2.0-rc.1` tag would move `:latest` to a release candidate, there is no rolling
major/minor tag to pin a Dockerfile's `FROM` against, and there is no channel for an in-development (`develop`)
build. The scheme to fix this was already agreed in the backlog stub (2026-08-14): rolling tags for stable
releases, an immutable-only tag for prereleases, and an `edge` channel for `develop`.

**Stale reference fixed**: the stub's "Notes" pointed at `add-release-changelog` as using "release-please" —
that item shipped with **git-cliff** instead (release-please cannot parse this repo's gitmoji commits). Doesn't
change this item's scope (both tools produce the same `v*` tag this item reacts to), just correcting the note.

## What Changes

- Extend the Nuke `DockerPublish` target (`build/Build.cs`) to compute its tag set from `VersionDetails`
  (already split into `PackageVersionPrefix`/`PackageVersionSuffix` by `GenerateVersionDetails`) instead of a
  single `--image-tag` string:
  - **Stable** (`PackageVersionSuffix` empty): `X.Y.Z` + `X.Y` + `X` + `latest`.
  - **Prerelease** (`PackageVersionSuffix` set): `X.Y.Z-<pre>` only — the rolling tags and `latest` are left
    untouched.
  - New `--edge` boolean flag: when set, also tags `edge` (used by the `develop`-triggered publish, below).
  - New `--publish-tag` **explicit override**: when set, publish exactly that one tag and skip the computed
    scheme entirely — preserves `base-image-refresh.yml`'s existing "republish `:latest` only" behavior
    unchanged (that workflow is not touched beyond a parameter rename).
- Add a `develop`-triggered job to `image.yml` (`push: branches: [develop]`) that calls the same `DockerPublish`
  target with `--edge` and **no** `--version` override, letting GitVersion resolve the prerelease version on
  `develop` the same way it already does for build-time stamping.
- The existing tag-triggered job switches from `--image-tag "$VERSION"` to `--version "$VERSION"` (already an
  existing Nuke parameter), so the computed scheme — not a single literal tag — decides what gets pushed.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: **MODIFIED** *Continuous image publishing* — the published tag set now depends on the release
  channel (stable / prerelease / edge) instead of unconditionally moving `latest`.

## Impact

- Modified: `build/Build.cs` (`DockerPublish` target, two new parameters), `.github/workflows/image.yml` (new
  job, one parameter rename on the existing job), `.github/workflows/base-image-refresh.yml` (one parameter
  rename, `--image-tag` → `--publish-tag`, behavior unchanged).
- No `src/`/`tests/` changes — build/CI-only (AG-DEP).
