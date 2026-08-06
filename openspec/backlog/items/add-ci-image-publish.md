---
id: add-ci-image-publish
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal — image distribution)
provenance: 2026-08-06 CI/ops backlog expansion
---

## Why
The application image (root `Dockerfile`) is only ever built locally (Nuke `DockerImage`/`DockerPublish`); it is
never published, so nobody can `docker pull` DockYarp. A CI job should build and push the image to a registry on
a release.

## Current state
- Root `Dockerfile` builds the app; Nuke `DockerImage` (`docker build -t dockyarp:local .`) + `DockerPublish`
  (`docker push`) exist but assume a manual, already-authenticated environment. No CI wiring, no registry.

## Proposed change (sketch)
- Add `.github/workflows/image.yml` triggered on a version tag (`v*`) and/or GitHub Release:
  - `docker/setup-buildx-action`; `docker/login-action` to **GHCR** (`ghcr.io/gcelet/dockyarp`) with
    `GITHUB_TOKEN` (`packages: write`).
  - `docker/build-push-action`: build + push, tags `:<version>` (from `add-release-versioning`) and `:latest`;
    **multi-arch** `linux/amd64,linux/arm64` (decide in design — buildx QEMU).
  - Provenance/SBOM attestation (buildx `provenance`/`sbom` flags) — or defer to `add-ci-security-scan`.
- Pin the `Dockerfile` `FROM` to a digest for reproducibility (ties into `add-base-image-rebuild`).

## Acceptance criteria (→ scenarios)
- **WHEN** a version tag is pushed (or a Release published) **THEN** the app image is built and pushed to GHCR
  tagged with the version and `latest`.
- **WHEN** multi-arch is enabled **THEN** the manifest lists `amd64` + `arm64`.

## Notes / risks / references
- Depends on `add-ci-build-test` (gate first) + `add-release-versioning` (the version). Registry = GHCR by
  default (Docker Hub is an alternative — decide).
- **Repo not created yet** → local dry-run with `act` + `--container-architecture`, or build locally with buildx
  and `--push=false`; a real registry push can't be fully simulated (inspect the manifest instead).
