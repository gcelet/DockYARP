## ADDED Requirements

### Requirement: Persistent state on a non-root-writable volume
DockYarp runs as a non-root user; its persistent state — ACME certificates and Data Protection keys — SHALL be
written to a mounted volume that the non-root app user can write and that survives container recreation, rather
than to the ephemeral container filesystem.

#### Scenario: Non-root app writes the mounted volume
- **WHEN** DockYarp (running non-root) provisions a certificate
- **THEN** the certificate is written to the mounted certificate volume without a permission error

#### Scenario: State survives container recreation
- **WHEN** the container is recreated with the same volume
- **THEN** previously persisted certificates and Data Protection keys are still present

#### Scenario: Data Protection keys are persisted
- **WHEN** DockYarp starts
- **THEN** Data Protection keys are stored under the certificate volume, not the ephemeral default location
