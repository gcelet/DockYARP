# Design — add-ci-image-publish

## Configurable registry (incl. an arbitrary private one)
The registry is not hardcoded. The workflow resolves:
- `registry` = `vars.IMAGE_REGISTRY` or `ghcr.io` (default).
- `repository` = `vars.IMAGE_REPOSITORY` or `github.repository` (lowercased — GHCR/OCI require lowercase).
- login credentials = `secrets.REGISTRY_USERNAME` / `secrets.REGISTRY_PASSWORD`, **falling back** to
  `github.actor` / `GITHUB_TOKEN` (the GHCR case).

So the default is GHCR with zero config; pointing at **any private registry** (Harbor, ACR, GitLab, Nexus,
self-hosted) is just setting `IMAGE_REGISTRY` + the two credential secrets — no workflow edit. `docker/login-action`
takes the resolved `registry` host, so a non-Docker-Hub private registry authenticates the same way.

## Injection safety
`inputs.tag` (from `workflow_dispatch`) and `github.ref_name` are attacker-influenced, so they are **never**
interpolated into a `run:` script. All external values are passed via `env:` and referenced as shell variables;
only trusted `steps.*.outputs` (produced by our own script into `$GITHUB_OUTPUT`) feed later `${{ }}` fields.

## Single build path (Nuke), not a duplicated workflow build
Building the image is owned by **one** place — the Nuke `DockerImage`/`DockerPublish` targets — used both locally
and by CI, so there is no separate `docker build` in the workflow:
- `DockerImage` → `docker buildx build --platform {Platforms} --load -t {FullImage} .` (single-arch, loaded into
  the local daemon for `docker run`/E2E). Gates on `Test`.
- `DockerPublish` → `docker buildx build --platform {Platforms} --push -t {FullImage} -t {LatestImage} .`
  (multi-arch push). Standalone (the CI gate runs separately on push).
- New `--platforms` parameter (default `linux/amd64`); the CI passes `linux/amd64,linux/arm64`.
The workflow sets up QEMU + buildx, `docker login`s to the resolved registry, then runs
`./build.sh DockerPublish --registry … --image-repository … --image-tag {version} --platforms linux/amd64,linux/arm64`.
- Tags: `{registry}/{repository}:{version}` + `:latest`; `version` = the `v*` tag stripped of `v` (or the
  `workflow_dispatch` input). Full version derivation is `add-release-versioning`.
- **arm64 via QEMU** emulates the SDK build stage (slower); acceptable for release builds. A per-RID `dotnet
  publish` optimization is a possible follow-up.

## Spec
ADD a `deployment` "Continuous image publishing" requirement (the existing "Image publishing" — the Nuke
`DockerPublish` pipeline, default Docker Hub, pre-authenticated — is left unchanged). The new requirement states
that publishing runs in CI on a release, to a configurable registry — GHCR by default, **or any other registry
including a private, non-Docker-Hub one**, authenticated with configured credentials — as a multi-architecture image.

## Out of scope
- SBOM/provenance attestation (→ `add-ci-security-scan`), digest-pinned `FROM` + rebuild (→ `add-base-image-rebuild`),
  semantic version derivation + GitHub Release notes (→ `add-release-versioning`).
