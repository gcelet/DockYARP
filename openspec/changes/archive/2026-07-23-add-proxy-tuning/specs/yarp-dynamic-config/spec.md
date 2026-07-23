## ADDED Requirements

### Requirement: Cluster request timeout
When a cluster defines a request timeout, the system SHALL apply it as the YARP activity timeout for that
cluster's outgoing requests; clusters without one keep the default.

#### Scenario: Cluster timeout maps to YARP
- **WHEN** a cluster defines a request timeout of 30 seconds
- **THEN** the mapped YARP cluster's activity timeout is 30 seconds

### Requirement: Per-route request body-size limit
When a route defines a maximum request body size, the system SHALL apply that limit to the request before it
is proxied; routes without one are unaffected.

#### Scenario: Body-size limit is applied
- **WHEN** a request matches a route with a maximum request body size
- **THEN** the request's maximum body size is set to that limit before proxying

#### Scenario: No limit leaves the request unchanged
- **WHEN** a request matches a route with no maximum request body size
- **THEN** the request's maximum body size is left unchanged
