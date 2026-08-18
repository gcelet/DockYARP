## MODIFIED Requirements

### Requirement: Continuous image publishing
On a release (a `v*` tag) or a push to the trunk branch, the system SHALL build and push the application Docker
image via CI to a **configurable** container registry — GitHub Container Registry (GHCR) by default, or **any
other registry, including a private, non-Docker-Hub registry** (for example a self-hosted Harbor, Azure
Container Registry, GitLab, or Nexus) — identified by a configurable registry host and repository,
authenticating with credentials supplied as secrets (defaulting to the GitHub token for GHCR). The published
image SHALL be a **multi-architecture** manifest (at least `linux/amd64` and `linux/arm64`), built from the
repository `Dockerfile` (whose build stage runs the Nuke build). The published **tag set** SHALL depend on the
release channel: a **stable** release (no pre-release suffix) SHALL push the exact version, its `Major.Minor`,
its `Major`, and `latest`; a **prerelease** (a `v*` tag with a pre-release suffix) SHALL push only its exact
version, leaving `latest` and the rolling `Major.Minor`/`Major` tags untouched; a push to the trunk branch (no
tag) SHALL push its GitVersion-resolved prerelease version plus an `edge` tag. **A release-tag publish SHALL
run the `Test` and `End-to-end test suite` gates first, in CI, and SHALL NOT push any image when either gate
fails; the trunk-branch (edge) publish is not subject to this gate.**

#### Scenario: Publish to the default registry (GHCR) on release
- **WHEN** a stable version tag `vX.Y.Z` is pushed and no custom registry is configured
- **THEN** the image is built and pushed to GHCR as `ghcr.io/{repository}:X.Y.Z`, `:X.Y`, `:X`, and `:latest`

#### Scenario: Prerelease tags do not move the rolling or latest tags
- **WHEN** a prerelease version tag `vX.Y.Z-<pre>` is pushed
- **THEN** the image is pushed only as `{repository}:X.Y.Z-<pre>`, and the `Major.Minor`/`Major`/`latest` tags
  are left pointing at whatever they previously pointed to

#### Scenario: A trunk push publishes the edge channel
- **WHEN** a commit is pushed to the trunk branch (no tag)
- **THEN** the image is pushed as `{repository}:edge` and as the GitVersion-resolved prerelease version for
  that commit (e.g. `{repository}:0.1.0-alpha.223`)

#### Scenario: Publish to a configured private registry
- **WHEN** a custom registry host and credentials are configured (`IMAGE_REGISTRY` + registry username/password
  secrets) and a release is published
- **THEN** the image is pushed as `{registry}/{repository}:{version}` (plus the rolling/latest tags for a
  stable release) on that private registry, authenticated with the supplied credentials

#### Scenario: Multi-architecture image
- **WHEN** the image is published
- **THEN** the pushed manifest includes at least `linux/amd64` and `linux/arm64`

#### Scenario: A failing test or e2e run blocks a release publish
- **WHEN** a `v*` release tag is pushed and either the `Test` gate or the end-to-end suite fails
- **THEN** CI fails before the `DockerPublish` step runs, and no image is pushed for that tag

#### Scenario: A trunk push is not gated by the end-to-end suite
- **WHEN** a commit is pushed to the trunk branch (no tag)
- **THEN** the edge image is published without waiting for the end-to-end suite (unit/integration coverage
  from ordinary CI still applies via the separate continuous-integration gate)
