## ADDED Requirements

### Requirement: Proxy tuning labels
The system SHALL read `DOCKYARP_PROXY_TIMEOUT` (a request timeout in seconds) into the cluster's request
timeout and `DOCKYARP_MAX_BODY_SIZE` (a maximum request body size in bytes) into the route's body-size
limit. Absent labels leave the values unset; a non-numeric or non-positive value is ignored (with a
warning).

#### Scenario: Proxy timeout label sets the cluster timeout
- **WHEN** a container declares `DOCKYARP_PROXY_TIMEOUT=30`
- **THEN** the mapped cluster's request timeout is 30 seconds

#### Scenario: Max body size label sets the route limit
- **WHEN** a container declares `DOCKYARP_MAX_BODY_SIZE=1048576`
- **THEN** the mapped route's maximum request body size is 1048576 bytes

#### Scenario: Invalid value is ignored
- **WHEN** a container declares `DOCKYARP_PROXY_TIMEOUT=soon`
- **THEN** the cluster has no request timeout and the invalid value is reported
