## MODIFIED Requirements

### Requirement: Resilient concurrent provisioning
The system SHALL provision certificates for multiple hosts concurrently, with a bounded degree of parallelism,
so that one host's slow or failing ACME validation does not delay or block provisioning for the other hosts.
Per-host failures SHALL remain isolated — logged, and never fatal to the pass or the other hosts. A per-host
provisioning failure that the reconcile loop resolves on a subsequent attempt (a **transient** failure, such as a
startup validation race) SHALL be logged at **Warning** with a short reason and **without** a stack trace; a failure
that **persists** across repeated attempts (beyond a small threshold of consecutive failures) SHALL escalate to
**Error** with the exception. A successful provisioning SHALL reset the host's consecutive-failure count.

#### Scenario: A slow host does not block others
- **WHEN** several hosts need certificates and one host's ACME validation is slow
- **THEN** the other hosts are provisioned without waiting for the slow one

#### Scenario: A failing host does not affect others
- **WHEN** one host's provisioning throws
- **THEN** the failure is logged and the other hosts are still provisioned

#### Scenario: Concurrency is bounded
- **WHEN** many hosts need certificates at once
- **THEN** the number of simultaneous ACME requests does not exceed the configured bound

#### Scenario: A transient failure is logged at Warning
- **WHEN** a host's provisioning fails but a later attempt succeeds
- **THEN** the transient failure is logged at Warning without a stack trace (not a misleading Error), and the host is
  ultimately provisioned

#### Scenario: A persistent failure escalates to Error
- **WHEN** a host's provisioning keeps failing beyond the transient threshold of consecutive attempts
- **THEN** the failure is logged at Error with the exception
