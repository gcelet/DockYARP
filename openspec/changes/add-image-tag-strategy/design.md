## Context

See `proposal.md` - Why. Relevant current state, checked before writing:
- `VersionDetails` (`build/VersionDetails.cs`) already splits the resolved version into
  `PackageVersionPrefix` (`Major.Minor.Patch`) and `PackageVersionSuffix` (empty for stable, the GitVersion
  `PreReleaseTag` otherwise) — exactly the split this scheme needs, with no new version-parsing logic required.
  `VersionDetails.Version` is already the full SemVer string (`GitVersion.SemVer`), Docker-tag-ready as-is (no
  `v` prefix, matching Docker convention).
- `DockerPublish` (`build/Build.cs`) currently pushes exactly `-t {FullImage} -t {LatestImage}`, where
  `FullImage`/`LatestImage` are built from the `ImageTag`/`Registry`/`ImageRepository` parameters — i.e. always
  exactly two tags, `latest` unconditional. This is the bug.
- `DockerImage` (local `--load`, used by `E2E`/`Release`) also uses `FullImage`/`ImageTag` — for a **local,
  single-tag** build, unrelated to the publish scheme. Not touched by this change.
- `base-image-refresh.yml` already calls `DockerPublish --image-tag latest` with **no** `--version` — it wants
  to republish exactly `:latest` with a patched base image, explicitly bypassing whatever GitVersion would
  resolve on `main` at that moment (which could be a stable release version, and must NOT move `X.Y.Z`/`X.Y`/`X`
  during a base-image patch). The stub itself flagged this as something to reconcile.
- `image.yml` currently triggers on `push: tags: ['v*']` + `workflow_dispatch`; the `develop`-push trigger for
  `edge` does not exist yet.

## Goals / Non-Goals

**Goals:**
- Fix the unconditional-`latest` bug and implement the full agreed scheme (stable rolling tags / prerelease
  exact-only / `edge` for trunk pushes) in the single Nuke path — the workflow only orchestrates.
- Leave `base-image-refresh.yml`'s existing "`:latest` only, don't touch `X.Y.Z`" behavior **unchanged**.

**Non-Goals:**
- A test/build gate on the `edge` publish path — `image.yml`'s tag-triggered job already publishes without a
  Nuke-level `Test` dependency (relies on `ci.yml` gating `develop`/PRs separately); adding an E2E gate on the
  published path is the explicit scope of the separate `add-e2e-release-gate` backlog item, not this one.
- Changing `DockerImage`'s local-build tagging (`ImageTag`, default `latest`) — untouched, unrelated to the
  publish scheme.
- Persisting/advertising the "Supported tags" list anywhere (registry README, docs) — that is
  `add-registry-readme-sync`'s scope, blocked on a separate decision (is Docker Hub even a target?).

## Decisions

- **`DockerPublish` computes its tag list from `VersionDetails`, not from `ImageTag`.** `ImageTag` stays exactly
  as-is (default `"latest"`) for `DockerImage`'s unrelated local-build use — reusing it for the publish scheme
  would silently change `DockerImage`'s default local tag if `ImageTag`'s default were changed to blank.
- **New `--publish-tag` parameter (default empty) is the explicit-override escape hatch**, not a repurposed
  `--image-tag`. When set, `DockerPublish` pushes exactly that one tag and skips the computed scheme —
  `base-image-refresh.yml` changes its call from `--image-tag latest` to `--publish-tag latest`; **zero
  behavior change**, just correct naming (it was never really "the image tag" in the publish sense, since
  `DockerPublish` also always force-added `latest`; now it precisely means "publish only this tag").
- **New `--edge` boolean parameter** — a flag, not inferred from the version shape, because a `develop` push and
  a manually-tagged prerelease can resolve to an identical-looking prerelease SemVer; only the *trigger* (which
  ref fired the workflow) knows whether `edge` should also move. The calling job sets it explicitly.
- **`image.yml` gets a second job, not a second workflow file.** Both jobs publish the same image via the same
  Nuke target; keeping them in one file under the existing "Publish image" name matches how the file already
  describes itself, and avoids proliferating near-duplicate workflow files. Trigger: add `branches: [develop]`
  to the existing `push` trigger; each job is scoped with an `if:` on `github.ref`/`github.event_name` (tag
  push and `workflow_dispatch` → the release job; a `develop` branch push → the edge job). Some orchestration
  steps (checkout, buildx setup, registry login) are duplicated between the two jobs — that is normal CI
  structure, not the "duplicated build logic" [[nuke-single-build-path]] guards against (both jobs still
  delegate the actual build/tag/push to the one Nuke target).
- **The edge job passes no `--version` override** — leaving `Version` empty means `GenerateVersionDetails` falls
  through to the GitVersion branch (same path already used for build-time stamping on every push), so the edge
  build's version tag is exactly what GitVersion resolves for that `develop` commit, with no duplicated logic.

## Risks / Trade-offs

- [A stable release's rolling `X.Y`/`X` tags could theoretically regress if an *older* patch within the same
  minor/major were re-tagged] → not addressed here (out of scope): this item computes tags from the version
  being published, same as any `major.minor`-rolling-tag scheme (e.g. `node:20`); re-publishing an old version
  intentionally moving `X.Y`/`X` backward would be an operator error, not something CI should second-guess.
- [`base-image-refresh.yml` depends on the exact `--image-tag` → `--publish-tag` rename] → both files are edited
  in this same change, so there is no window where one is updated and the other isn't.
- [Every `develop` push now triggers a full multi-arch `buildx --push`] → matches the stub's explicit design
  ("In-development push to develop → push a bleeding-edge tag"); acceptable given push cadence is already
  batched (evening pushes), not a many-times-a-day trigger.

## Migration Plan

No migration — additive to the Nuke build (new parameters, existing ones untouched) and CI-only. Rollback is
reverting the `DockerPublish` target and the two workflow files; nothing else depends on the new parameters.
