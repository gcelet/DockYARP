## ADDED Requirements

### Requirement: Build versioning
The build SHALL derive its version from git (base version + commit height + `v*` tags) using GitVersion — declared
as the local .NET tool `gitversion.tool` in `.config/dotnet-tools.json` and configured by a root `GitVersion.yml` —
computing the version **once on the host** in the Nuke build and stamping the assemblies
(`AssemblyVersion`/`FileVersion`/`InformationalVersion`, the latter including the commit id). The running build's
version SHALL be exposed by the Admin API. Because the Docker build context excludes `.git`, the image SHALL be
stamped with the **same** host-computed version, passed to the build as a build argument, without running GitVersion
inside the container.

#### Scenario: Assemblies stamped from git
- **WHEN** the build runs in a git working tree
- **THEN** the produced assemblies' informational version reflects the GitVersion-derived version (base version +
  git height + commit id)

#### Scenario: Admin API reports the version
- **WHEN** a client calls `GET /api/version` (with a valid API key)
- **THEN** the response reports the running build's version

#### Scenario: Image stamped without .git in the context
- **WHEN** the Docker image is built (its context excludes `.git`)
- **THEN** the host-computed version is passed as a build argument and the app inside the image reports that same
  version
