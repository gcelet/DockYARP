## MODIFIED Requirements

### Requirement: End-to-end test suite
The system SHALL provide an end-to-end test suite that boots DockYarp and labeled backend containers on a
real Docker daemon (via .NET Aspire) and asserts, over HTTP, that containers are discovered and requests are
proxied according to their labels. The suite SHALL be runnable through the build pipeline and included in
release validation, and SHALL be excluded from the ordinary build/test so the default developer loop needs no
Docker daemon. The default build/test SHALL exclude the end-to-end suite by project (not by a category filter
that matches no tests) so it runs deterministically.

#### Scenario: End-to-end suite excluded from the default build
- **WHEN** the default build/test target runs (no explicit end-to-end request)
- **THEN** the end-to-end tests do not execute and no Docker daemon is required

#### Scenario: Default build/test runs deterministically
- **WHEN** the default build/test target runs
- **THEN** it runs the unit/integration test projects (excluding the end-to-end project) and does not fail on
  projects that match no tests

#### Scenario: End-to-end suite runnable on demand
- **WHEN** the dedicated end-to-end target is invoked with a Docker daemon available
- **THEN** the `dockyarp:local` image is built, the Aspire application boots DockYarp with the labeled
  backend containers, and the end-to-end tests run against it

#### Scenario: Release validation runs the end-to-end suite
- **WHEN** the release target runs
- **THEN** it depends on both the ordinary test suite and the end-to-end suite, so a release is validated only
  when the end-to-end tests also pass

#### Scenario: Discovered backend is reachable through the proxy
- **WHEN** the Aspire application is running and a request is sent to DockYarp with a backend container's
  `VIRTUAL_HOST`
- **THEN** the request is proxied to that backend and returns its response
