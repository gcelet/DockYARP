## ADDED Requirements

### Requirement: Host-network backends
The system SHALL support backends running in Docker host network mode. A host-network container (identified by
a reserved `host` network entry) has no container IP, so the system SHALL forward to the configured Docker host
address (`Docker:HostAddress`) on the backend's port. A host-network backend SHALL require `VIRTUAL_PORT`
(no port can be inferred). When `Docker:HostAddress` is not configured, a host-network backend SHALL be skipped
with a warning rather than routed.

#### Scenario: Host-network backend is routed to the host address
- **WHEN** a host-network container sets `VIRTUAL_HOST` and `VIRTUAL_PORT` and `Docker:HostAddress` is configured
- **THEN** its cluster targets the configured host address on that port

#### Scenario: Host-network backend without a host address is skipped
- **WHEN** a host-network container is discovered and `Docker:HostAddress` is not configured
- **THEN** it is not routed and a warning indicates `Docker:HostAddress` must be set

#### Scenario: Host-network backend without VIRTUAL_PORT is skipped
- **WHEN** a host-network container sets `VIRTUAL_HOST` but no `VIRTUAL_PORT` (and exposes no single port)
- **THEN** it is not routed and a warning indicates `VIRTUAL_PORT` is required
