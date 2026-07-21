# deployment Specification

## Purpose
TBD - created by archiving change add-deployment. Update Purpose after archive.
## Requirements
### Requirement: Docker image
The system SHALL be packaged as a minimal multi-stage Docker image whose build stage runs the Nuke build
pipeline and whose runtime stage is a chiseled .NET runtime, running as a non-root user, exposing the proxy
ports and supporting mounted `/certs` and `/config` volumes.

#### Scenario: Container starts with mounted volumes
- **WHEN** the image is started with volumes mounted for certificates and configuration
- **THEN** DockYarp starts and reads certificates and configuration from those volumes

### Requirement: Reference Compose stack
The system SHALL provide a reference `docker-compose.yml` demonstrating label-based configuration for at
least one sample service reachable through DockYarp by its `VIRTUAL_HOST`.

#### Scenario: Sample service reachable through the proxy
- **WHEN** the reference stack is started and a request is sent to DockYarp with the sample service's `VIRTUAL_HOST`
- **THEN** the request is proxied to the sample service and returns its response

### Requirement: Graceful shutdown
The system SHALL shut down gracefully on a termination signal, draining in-flight requests and stopping
background workers (discovery, provisioning) cleanly within a bounded timeout.

#### Scenario: In-flight requests are drained
- **WHEN** the container receives a stop signal while requests are in flight
- **THEN** in-flight requests are allowed to complete within the shutdown timeout before the process exits

