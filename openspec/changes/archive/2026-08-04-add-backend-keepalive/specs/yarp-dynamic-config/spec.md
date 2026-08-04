## ADDED Requirements

### Requirement: Per-cluster backend connection limit
When a cluster defines a maximum number of backend connections (`DOCKYARP_MAX_CONNECTIONS`), the system SHALL
apply it as the YARP cluster's `HttpClientConfig.MaxConnectionsPerServer`; a cluster without one keeps YARP's
default backend connection pooling. An invalid (non-positive) value SHALL be ignored with a warning, leaving
the default.

#### Scenario: Connection limit maps to YARP
- **WHEN** a cluster defines `DOCKYARP_MAX_CONNECTIONS=64`
- **THEN** the mapped YARP cluster's `HttpClientConfig.MaxConnectionsPerServer` is 64

#### Scenario: No limit leaves pooling unchanged
- **WHEN** a cluster defines no `DOCKYARP_MAX_CONNECTIONS`
- **THEN** the mapped YARP cluster has no `HttpClient` override and uses YARP's default pooling

#### Scenario: Invalid value is ignored
- **WHEN** a cluster defines `DOCKYARP_MAX_CONNECTIONS=0` or a non-numeric value
- **THEN** the value is ignored, a warning is reported, and the cluster keeps YARP's default pooling
