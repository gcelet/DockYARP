## ADDED Requirements

### Requirement: Continuous documentation build and publish
The documentation site SHALL be built **reproducibly** — with a pinned Hugo Extended toolchain and no ambient
dependency — via a dedicated build target, and SHALL be published to a static host (GitHub Pages by default) by CI:
a build check on every pull request, and a build-and-publish on every push to the default development branch. The
documentation build SHALL be isolated from the application build so the two do not cross-contaminate.

#### Scenario: Reproducible local/CI build
- **WHEN** the documentation build target runs
- **THEN** it installs the pinned Hugo Extended toolchain and produces a complete static site, with no reliance on an
  ambiently-installed Hugo

#### Scenario: Published on push to the development branch
- **WHEN** a commit that changes the documentation is pushed to the default development branch
- **THEN** CI builds the site and publishes it to GitHub Pages

#### Scenario: Build check on a pull request
- **WHEN** a pull request changes the documentation
- **THEN** CI builds the site (without publishing) and fails the check if the build fails
