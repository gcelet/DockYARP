# Design — add-base-image-rebuild

## Trigger
`base-image-refresh.yml` on `push` to the default branch filtered to `paths: [Dockerfile]`, plus
`workflow_dispatch` for a manual refresh. A merged Renovate digest-bump PR (Renovate pins the `FROM` digest per
`add-renovate-bot`) changes the `Dockerfile`, which fires this workflow.

## What it does
Mirrors `image.yml` (registry/repository resolution via `vars.IMAGE_REGISTRY`/`IMAGE_REPOSITORY`, login via
`REGISTRY_USERNAME`/`REGISTRY_PASSWORD` or GHCR defaults, QEMU + Buildx) but **republishes only `:latest`** — it
delegates to `./build.sh DockerPublish --image-tag latest`, the single Nuke image path, which buildx-builds the
multi-arch manifest with the patched base and pushes it.

- **`fetch-depth: 0`** on checkout: `DockerPublish` runs the Nuke build, which computes the GitVersion version
  (`add-release-versioning`); GitVersion needs the full history/tags, so a shallow clone would break it.
- External values (registry/repository) flow through `env:`, never interpolated into `run:` (injection-safe, same
  as `image.yml`).

## Immutability
Released `v*` tags are **not** re-pushed (immutable). A base patch updates `:latest`; consumers pinned to a version
receive the patched base at the next tagged release. This avoids the anti-pattern of mutating a published release
tag's contents.

## Scheduled net — out of scope (decided)
The stub floated an optional daily cron that diffs the pinned digest against the registry. It is **declined**:
Renovate already watches and bumps the `FROM` digest, so a cron would duplicate that and add digest-diff logic to
maintain. If Renovate's weekly cadence proves too slow for urgent CVEs, revisit by tightening Renovate's schedule
for the `docker` manager rather than adding a parallel mechanism.

## Out of scope
- Re-tagging/patching prior `v*` releases (immutability, above).
- Any change to `image.yml` (tag-driven release publishing is unchanged).
