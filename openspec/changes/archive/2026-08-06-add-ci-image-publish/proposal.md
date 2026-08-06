## Why
The application image (root `Dockerfile`) is only ever built locally (Nuke `DockerImage`/`DockerPublish`); it is
never published, so nobody can pull DockYarp. A CI job should build and push the image on a release — and it must
support publishing to a **private registry other than Docker Hub** (GHCR, a self-hosted Harbor, Azure Container
Registry, GitLab, Nexus…), not only Docker Hub.

## What Changes
- Make the **Nuke build the single image-build path** (local + CI — no build logic duplicated in the workflow):
  - `DockerImage` and `DockerPublish` switch from plain `docker build`/`push` to **`docker buildx`**, with a new
    `--platforms` parameter. `DockerImage` `--load`s a single-arch image locally (for `docker run`/the E2E stack);
    `DockerPublish` `--push`es a **multi-arch** manifest and also tags `:latest`.
- Add `.github/workflows/image.yml`, triggered on a version tag (`v*`) and `workflow_dispatch`, which sets up
  buildx, logs in, and **delegates to `./build.sh DockerPublish`** with the resolved parameters:
  - The target registry is **fully configurable**: `vars.IMAGE_REGISTRY` (host, default `ghcr.io`) +
    `vars.IMAGE_REPOSITORY` (default `github.repository`). Credentials come from `secrets.REGISTRY_USERNAME` /
    `secrets.REGISTRY_PASSWORD`, defaulting to `github.actor` + `GITHUB_TOKEN` for GHCR — so **any private
    registry** works by setting those secrets, with no change to the workflow.
  - `--platforms linux/amd64,linux/arm64`; tags `:{version}` (derived from the `v*` tag) + `:latest`.
  - Injection-safe: external values (`github.ref_name`, `inputs.tag`, `vars.*`) flow through `env:` and are only
    referenced as shell variables, never interpolated into `run:` scripts.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: image publishing runs in CI on a release, to a configurable registry (default GHCR; **any
  private, non-Docker-Hub registry** supported via secrets), building a multi-architecture image.

## Impact
- **Code**: `.github/workflows/image.yml` (new); `build/Build.cs` — `DockerImage`/`DockerPublish` reworked to
  `docker buildx` (`--load` local, `--push` multi-arch) with a `--platforms` parameter. No app code.
- **Validation**: no repo/registry yet → local dry-run with `act` and `docker buildx build --push=false`, plus
  YAML/`actionlint`; a real push waits for the repo + registry credentials.
- **Owning agent**: AG-DEP. Resolves `add-ci-image-publish` (depends on `add-ci-build-test`; the version is
  refined by `add-release-versioning`).
