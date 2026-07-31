## MODIFIED Requirements

### Requirement: Per-cluster load balancing
The system SHALL support YARP's built-in load-balancing policies, configurable per cluster via `DOCKYARP_LB`:
round-robin (default), least-requests, power-of-two-choices, random, and first-alphabetical. An unrecognized
`DOCKYARP_LB` value SHALL fall back to round-robin and SHALL be reported as a warning.

#### Scenario: Round-robin distributes requests
- **WHEN** a cluster has two healthy endpoints and round-robin is configured
- **THEN** consecutive requests alternate between the two endpoints

#### Scenario: A built-in policy is selected
- **WHEN** a container sets `DOCKYARP_LB=power-of-two-choices`
- **THEN** its cluster uses YARP's power-of-two-choices policy

#### Scenario: Unknown policy falls back with a warning
- **WHEN** a container sets an unrecognized `DOCKYARP_LB` value
- **THEN** the cluster uses round-robin and a warning is reported
