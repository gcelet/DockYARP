## ADDED Requirements

### Requirement: Docker image
The system SHALL be packaged as a minimal multi-stage Docker image that exposes the proxy ports and
supports mounting `/certs` and `/config` volumes.

#### Scenario: Container starts with mounted volumes
- **WHEN** the image is started with volumes mounted for certificates and configuration
- **THEN** DockYarp starts and reads certificates and configuration from those volumes

### Requirement: Reference Compose stack
The system SHALL provide a reference `docker-compose.yml` demonstrating label-based configuration and
automatic TLS for at least one sample service.

#### Scenario: Sample service exposed with HTTPS
- **WHEN** the reference stack is started
- **THEN** the sample service is reachable through DockYarp over HTTPS with a valid certificate

### Requirement: Graceful shutdown
The system SHALL shut down gracefully on container stop, draining in-flight requests and stopping
background workers (discovery, renewal) cleanly within a bounded timeout.

#### Scenario: In-flight requests are drained
- **WHEN** the container receives a stop signal while requests are in flight
- **THEN** in-flight requests are allowed to complete within the shutdown timeout before the process exits
