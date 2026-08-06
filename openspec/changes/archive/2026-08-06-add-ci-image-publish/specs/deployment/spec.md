## ADDED Requirements

### Requirement: Continuous image publishing
On a release (a `v*` tag), the system SHALL build and push the application Docker image via CI to a
**configurable** container registry — GitHub Container Registry (GHCR) by default, or **any other registry,
including a private, non-Docker-Hub registry** (for example a self-hosted Harbor, Azure Container Registry,
GitLab, or Nexus) — identified by a configurable registry host and repository, authenticating with credentials
supplied as secrets (defaulting to the GitHub token for GHCR). The published image SHALL be a
**multi-architecture** manifest (at least `linux/amd64` and `linux/arm64`) tagged with the release version and
`latest`, and SHALL be built from the repository `Dockerfile` (whose build stage runs the Nuke build).

#### Scenario: Publish to the default registry (GHCR) on release
- **WHEN** a version tag `vX.Y.Z` is pushed and no custom registry is configured
- **THEN** the image is built and pushed to GHCR as `ghcr.io/{repository}:X.Y.Z` and `:latest`

#### Scenario: Publish to a configured private registry
- **WHEN** a custom registry host and credentials are configured (`IMAGE_REGISTRY` + registry username/password
  secrets) and a release is published
- **THEN** the image is pushed to `{registry}/{repository}:{version}` on that private registry, authenticated
  with the supplied credentials

#### Scenario: Multi-architecture image
- **WHEN** the image is published
- **THEN** the pushed manifest includes at least `linux/amd64` and `linux/arm64`
