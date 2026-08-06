## ADDED Requirements

### Requirement: Base image refresh
When the base image referenced by the `Dockerfile` changes, the system SHALL rebuild and republish the `:latest`
application image with the patched base, via CI delegating to the single Nuke image-publish path, so the published
`:latest` does not accumulate unpatched base CVEs. Released version tags (`v*`) SHALL remain immutable — a base
patch updates `:latest`, and a tagged release picks up the patched base at publish time.

#### Scenario: Base-image update republishes latest
- **WHEN** a change to the `Dockerfile` base image (e.g. a merged Renovate digest bump) lands on the default branch
- **THEN** the application image is rebuilt and `:latest` is republished with the patched base (multi-architecture)

#### Scenario: Released tags stay immutable
- **WHEN** the base image is patched between releases
- **THEN** existing `v*` image tags are not re-pushed; the patched base reaches versioned images at the next release
