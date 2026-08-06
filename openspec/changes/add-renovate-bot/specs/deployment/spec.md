## ADDED Requirements

### Requirement: Automated dependency updates
The project SHALL keep its dependencies current via Renovate, configured to cover the NuGet Central Package
Management versions (`Directory.Packages.props`), the GitHub Actions workflows, the `Dockerfile` base image
(pinned to a digest and kept updated), and the docs-site npm packages — opening **grouped** update pull requests
that the CI gate validates. The relaxed .NET SDK floor in `global.json` (any installed .NET 10) SHALL NOT be
re-pinned by Renovate.

#### Scenario: Grouped dependency update PRs
- **WHEN** a covered dependency (a NuGet package, a GitHub Action, the base image, or an npm package) has an update
- **THEN** Renovate opens a pull request per the grouping policy, which the CI gate validates

#### Scenario: SDK floor left relaxed
- **WHEN** the .NET SDK has a newer release
- **THEN** Renovate does not re-pin `global.json` (the any-.NET-10 floor is preserved)
