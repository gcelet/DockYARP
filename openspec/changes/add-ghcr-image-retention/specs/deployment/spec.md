## ADDED Requirements

### Requirement: Scheduled GHCR image retention
The system SHALL prune superseded edge-channel image tags from the container registry on a recurring
schedule, so per-commit prerelease tags do not accumulate indefinitely once superseded. Every stable release
tag (exact version, `Major.Minor`, `Major`, and `latest`) and the `edge` tag itself SHALL always be retained,
regardless of age. Only tags matching the GitVersion-resolved edge-prerelease shape (containing a hyphen
separator) SHALL be eligible for deletion, and only once older than a configured minimum age. The retention
process SHALL NOT delete a per-platform manifest that a retained multi-architecture tag still references.

#### Scenario: Release and edge tags are never pruned
- **WHEN** the scheduled retention run executes
- **THEN** every stable release tag (`X.Y.Z`, `X.Y`, `X`, `latest`) and `edge` remain in the registry,
  regardless of how old they are

#### Scenario: Superseded edge prerelease tags are pruned
- **WHEN** the scheduled retention run executes and a GitVersion-resolved edge prerelease tag (e.g.
  `0.1.0-alpha.223`) is older than the configured minimum age
- **THEN** that tag is deleted from the registry

#### Scenario: A retained multi-architecture tag stays pullable after pruning
- **WHEN** the scheduled retention run deletes superseded tags
- **THEN** every retained tag's `linux/amd64` and `linux/arm64` manifests are still present and that tag still
  pulls and runs correctly
