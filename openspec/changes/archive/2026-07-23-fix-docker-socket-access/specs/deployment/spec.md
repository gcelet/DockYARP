## MODIFIED Requirements

### Requirement: Reference Compose stack
The system SHALL provide a reference `docker-compose.yml` demonstrating label-based configuration for at
least one sample service reachable through DockYarp by its `VIRTUAL_HOST`. DockYarp SHALL run as a
non-root container and, by default, reach the Docker API through a socket-proxy service rather than mounting
the Docker socket directly.

#### Scenario: Sample service reachable through the proxy
- **WHEN** the reference stack is started and a request is sent to DockYarp with the sample service's `VIRTUAL_HOST`
- **THEN** the request is proxied to the sample service and returns its response

#### Scenario: Discovery works without mounting the socket into DockYarp
- **WHEN** the reference stack is started
- **THEN** DockYarp discovers containers through the socket-proxy endpoint while running as a non-root
  container that does not mount the Docker socket

## ADDED Requirements

### Requirement: Non-root Docker API access
The system SHALL support Docker discovery from a non-root DockYarp container through two documented modes:
a socket proxy (the recommended default) exposing a read-only Docker API over TCP, and, as an alternative,
mounting the Docker socket while granting the container membership of the socket's owning group.

#### Scenario: Access via a socket proxy (default)
- **WHEN** DockYarp is configured with a Docker endpoint pointing at a socket-proxy service
- **THEN** discovery reaches the Docker API over that endpoint without DockYarp mounting the socket or
  running as root

#### Scenario: Access via group membership (alternative)
- **WHEN** the alternative example mounts the Docker socket and adds the container to the socket's owning group
- **THEN** the non-root DockYarp process can read the socket and discovery works
