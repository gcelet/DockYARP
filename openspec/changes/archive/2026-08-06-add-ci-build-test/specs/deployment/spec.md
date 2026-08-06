## ADDED Requirements

### Requirement: Continuous integration
The project SHALL run its build-and-test gate automatically on every pull request and on every push to the
default branch, via a GitHub Actions workflow that checks out the repository, sets up the .NET SDK pinned by
`global.json`, restores and compiles the whole solution, and runs the Nuke `Test` gate (unit + integration
tests, warnings treated as errors). A build warning or a test failure SHALL fail the check. The end-to-end
suite (which requires a Docker daemon) is out of scope of this gate.

#### Scenario: CI runs on a pull request
- **WHEN** a pull request is opened or updated
- **THEN** the workflow restores, compiles the solution, and runs the Nuke `Test` gate, failing the check on any
  build warning or test failure

#### Scenario: CI runs on push to the default branch
- **WHEN** a commit is pushed to the default branch
- **THEN** the same build-and-test gate runs
