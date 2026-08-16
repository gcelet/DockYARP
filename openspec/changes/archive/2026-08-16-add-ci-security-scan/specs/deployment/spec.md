## ADDED Requirements

### Requirement: Supply-chain security scanning
The project SHALL run static code analysis (CodeQL) on every pull request and on a recurring schedule, SHALL
review dependency changes on every pull request for known vulnerabilities, and SHALL generate a Software Bill
of Materials and run a vulnerability scan against every published container image, failing the build on a
Critical or High severity finding unless explicitly allowlisted.

#### Scenario: CodeQL runs on a pull request and on a schedule
- **WHEN** a pull request is opened or updated, or the weekly schedule fires
- **THEN** CodeQL analyzes the C# codebase and surfaces any findings as code scanning alerts

#### Scenario: Dependency review flags vulnerable or newly-added dependencies
- **WHEN** a pull request changes a dependency manifest
- **THEN** the pull request check fails if a changed dependency introduces a known vulnerability at or above the
  configured severity threshold

#### Scenario: Every published image is scanned and gets an SBOM
- **WHEN** the application image is published (release or edge channel)
- **THEN** a Software Bill of Materials is generated for that image and a vulnerability scan runs against it,
  failing the workflow on a Critical or High severity finding that is not allowlisted

#### Scenario: An accepted finding can be allowlisted
- **WHEN** a vulnerability finding is deliberately accepted (no fix available, not exploitable in this context)
- **THEN** it can be added to a tracked allowlist so it no longer fails the build, without disabling the scan
