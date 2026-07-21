## ADDED Requirements

### Requirement: Read-only admin endpoints
The system SHALL expose read-only endpoints `/api/routes`, `/api/clusters`, `/api/certs`, and
`/api/health` returning the current state as JSON, and SHALL NOT expose secrets such as Basic Auth
passwords.

#### Scenario: Routes endpoint returns active configuration
- **WHEN** an authorized client issues `GET /api/routes`
- **THEN** the response is the current active routing configuration as JSON

#### Scenario: Health endpoint reports status
- **WHEN** an authorized client issues `GET /api/health`
- **THEN** the response reports overall system health status

#### Scenario: Secrets are not exposed
- **WHEN** an authorized client reads `/api/routes` for a route protected by Basic Auth
- **THEN** the response indicates the route requires auth but does not include the password

### Requirement: Admin API protection
The system SHALL protect the admin API with an API key supplied in a request header, rejecting requests
with a missing or invalid key with 401.

#### Scenario: Unauthorized request is rejected
- **WHEN** a request to an admin endpoint lacks a valid API key
- **THEN** the response status is 401 and no state is returned

#### Scenario: Authorized request succeeds
- **WHEN** a request presents the configured API key
- **THEN** the endpoint returns its data

### Requirement: Observability
The system SHALL emit structured logs and expose a metrics endpoint (`/metrics`) in Prometheus format with
operational metrics such as the number of active routes and clusters.

#### Scenario: Metrics endpoint is scrapable
- **WHEN** a client issues `GET /metrics`
- **THEN** the response is a Prometheus exposition a scraper can parse
