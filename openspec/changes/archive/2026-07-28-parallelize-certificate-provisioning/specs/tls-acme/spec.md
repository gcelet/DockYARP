## ADDED Requirements

### Requirement: Resilient concurrent provisioning
The system SHALL provision certificates for multiple hosts concurrently, with a bounded degree of parallelism,
so that one host's slow or failing ACME validation does not delay or block provisioning for the other hosts.
Per-host failures SHALL remain isolated — logged, and never fatal to the pass or the other hosts.

#### Scenario: A slow host does not block others
- **WHEN** several hosts need certificates and one host's ACME validation is slow
- **THEN** the other hosts are provisioned without waiting for the slow one

#### Scenario: A failing host does not affect others
- **WHEN** one host's provisioning throws
- **THEN** the failure is logged and the other hosts are still provisioned

#### Scenario: Concurrency is bounded
- **WHEN** many hosts need certificates at once
- **THEN** the number of simultaneous ACME requests does not exceed the configured bound
