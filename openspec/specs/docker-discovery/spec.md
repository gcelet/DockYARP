# docker-discovery Specification

## Purpose
TBD - created by archiving change add-docker-discovery. Update Purpose after archive.
## Requirements
### Requirement: Docker events subscription
The system SHALL subscribe to the Docker daemon event stream and react to container `start`, `stop`,
`die`, and `update` events by updating the dynamic configuration accordingly.

#### Scenario: Labeled container starts
- **WHEN** a container carrying `VIRTUAL_HOST=app.local` is started
- **THEN** discovery creates/updates the corresponding route and cluster endpoint for `app.local`

#### Scenario: Container stops
- **WHEN** a running, routed container stops or dies
- **THEN** discovery removes that container's endpoint from its cluster

### Requirement: Resilient event connection
The system SHALL maintain the Docker event subscription across daemon restarts and transient connection
failures by reconnecting automatically, and SHALL reconcile state after reconnecting so no change is lost.

#### Scenario: Daemon restarts
- **WHEN** the Docker daemon connection drops and later becomes available again
- **THEN** discovery reconnects to the event stream without operator intervention

#### Scenario: Reconcile after reconnect
- **WHEN** discovery reconnects after an outage during which containers changed
- **THEN** it re-enumerates running containers so the active configuration matches reality

### Requirement: Startup reconciliation
The system SHALL, at startup, enumerate already-running containers and build the initial configuration
from their labels, independently of any future events.

#### Scenario: Container started before DockYarp
- **WHEN** a labeled container is already running when DockYarp starts
- **THEN** discovery routes it during startup without waiting for a new Docker event

### Requirement: Label schema and parsing
The system SHALL parse nginx-proxy-compatible labels (`VIRTUAL_HOST`, `VIRTUAL_PORT`, `VIRTUAL_PATH`,
`LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL`) and `DOCKYARP_*` labels into a strongly-typed configuration
object, applying documented defaults (e.g. a default target port when `VIRTUAL_PORT` is absent).

#### Scenario: Host and port produce a routable target
- **WHEN** a container declares `VIRTUAL_HOST=app.local` and `VIRTUAL_PORT=8080`
- **THEN** the parsed configuration targets that container on port 8080 for host `app.local`

#### Scenario: Default port when VIRTUAL_PORT is absent
- **WHEN** a container declares `VIRTUAL_HOST=app.local` with no `VIRTUAL_PORT` and exposes a single port
- **THEN** the parsed configuration targets that single exposed port

### Requirement: Label validation with safe fallback
The system SHALL validate label combinations and, on invalid or conflicting labels, log a structured
error and ignore the offending container without crashing or affecting other containers.

#### Scenario: Invalid label combination is ignored
- **WHEN** a container declares `VIRTUAL_PORT` but no `VIRTUAL_HOST`
- **THEN** the container is skipped, a structured warning is logged, and other containers remain routed

#### Scenario: One bad container does not stop discovery
- **WHEN** one container has invalid labels among several valid ones
- **THEN** the valid containers are still routed and only the invalid one is skipped and logged

### Requirement: Mapping to the routing model
The system SHALL translate parsed container configuration and container network information into
`proxy-routing` routes, clusters, endpoints, and per-host TLS metadata, and publish them as the dynamic
configuration source so the active snapshot updates without a process restart.

#### Scenario: Second replica joins the cluster
- **WHEN** a second container with the same `VIRTUAL_HOST` is started
- **THEN** its endpoint is added to the existing cluster for that host rather than creating a new route

#### Scenario: LETSENCRYPT labels populate TLS metadata
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `LETSENCRYPT_EMAIL=admin@example.com`
- **THEN** the mapped route carries TLS metadata requesting a certificate for `app.local` with that email

### Requirement: Basic Auth labels
The system SHALL read `DOCKYARP_AUTH_USER`, `DOCKYARP_AUTH_PASSWORD`, and optional
`DOCKYARP_AUTH_REALM` from a container's labels and populate the route's Basic Auth credentials, so the
security layer can protect the route. Incomplete auth labels SHALL be logged and leave the route
unprotected without failing discovery.

#### Scenario: Auth labels protect the route
- **WHEN** a container declares `DOCKYARP_AUTH_USER=admin` and `DOCKYARP_AUTH_PASSWORD=secret`
- **THEN** the mapped route carries those Basic Auth credentials (with the realm if provided)

#### Scenario: Incomplete auth labels are ignored safely
- **WHEN** a container declares `DOCKYARP_AUTH_USER` but no `DOCKYARP_AUTH_PASSWORD`
- **THEN** the route is left unprotected and a warning is logged, and other containers are unaffected

### Requirement: Multiple hosts per container
The system SHALL accept a comma-separated `VIRTUAL_HOST` and map the container to one route per host, each
sharing the container's port, path, TLS, and auth settings. Empty entries SHALL be ignored.

#### Scenario: Comma-separated hosts create multiple routes
- **WHEN** a container declares `VIRTUAL_HOST=app.local,www.app.local`
- **THEN** routes are created for both `app.local` and `www.app.local` targeting the container

#### Scenario: Whitespace and empty entries are tolerated
- **WHEN** a container declares `VIRTUAL_HOST=a.local, ,b.local`
- **THEN** routes are created for `a.local` and `b.local` and the empty entry is ignored

