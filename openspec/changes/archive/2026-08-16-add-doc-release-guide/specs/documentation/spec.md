## ADDED Requirements

### Requirement: Release process reference
The documentation site SHALL provide a standalone page walking a contributor through cutting a release: the
one-time bootstrap step of creating the `main` branch for the first release (merging `develop` in and tagging
`v0.1.0`); how to read the version GitVersion would compute before tagging; the exact command to push a release
tag; and a summary of what happens automatically afterward (changelog generation and GitHub Release creation,
tagged image publish), linking to the authoritative workflow files rather than duplicating their behavior. The
release process SHALL be documented in exactly one place on the site.

#### Scenario: Contributor finds the release steps in one place
- **WHEN** a contributor opens the Releasing page
- **THEN** they find, in order, the version-check step, the tag command, and a summary of what runs
  automatically after the tag is pushed — without needing to read `GitVersion.yml` or the workflow YAML directly

#### Scenario: First-release bootstrap is covered
- **WHEN** a contributor reads the Releasing page before any release has been cut
- **THEN** the page explicitly describes the one-time step of creating `main` from `develop` and tagging `v0.1.0`

#### Scenario: Release process is not duplicated elsewhere
- **WHEN** the Contributing page mentions releases
- **THEN** it points to the Releasing page rather than restating the steps
