## ADDED Requirements

### Requirement: Health-aware endpoint selection
The system SHALL exclude a container from routing while its Docker health status is `unhealthy` or
`starting`, and SHALL route to a container that is `healthy` or declares no health check. When several
containers share a host, an excluded container SHALL NOT remove the healthy siblings from the cluster.

#### Scenario: Healthy container is routed
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` reports health `healthy`
- **THEN** its endpoint is added to the `app.local` cluster

#### Scenario: Unhealthy container is excluded
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` reports health `unhealthy`
- **THEN** no endpoint is added for it and the exclusion is reported

#### Scenario: Container without a health check is routed
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` has no health check
- **THEN** its endpoint is added to the `app.local` cluster

#### Scenario: Healthy sibling still serves the host
- **WHEN** two containers share `VIRTUAL_HOST=app.local` and one is `unhealthy` while the other is `healthy`
- **THEN** the `app.local` cluster contains only the healthy container's endpoint

### Requirement: React to health transitions
The system SHALL treat a Docker `health_status` event as a trigger to re-evaluate discovery, so a container
that becomes healthy is added and one that becomes unhealthy is removed.

#### Scenario: Health-status event triggers reconciliation
- **WHEN** the Docker daemon emits a `health_status` event for a container
- **THEN** discovery re-evaluates the running containers and updates the routing configuration
