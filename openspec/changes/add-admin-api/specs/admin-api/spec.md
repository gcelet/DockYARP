## ADDED Requirements

### Requirement: Read-only admin endpoints
The system SHALL expose read-only endpoints `/api/routes`, `/api/clusters`, `/api/certs`, and
`/api/health` returning the current state as JSON.

#### Scenario: Routes endpoint returns active configuration
- **WHEN** a client issues `GET /api/routes`
- **THEN** the response is the current active routing configuration as JSON

#### Scenario: Health endpoint reports status
- **WHEN** a client issues `GET /api/health`
- **THEN** the response reports overall system health status

### Requirement: Admin API protection
The system SHALL protect the admin API with a token or IP allowlist, rejecting unauthorized requests.

#### Scenario: Unauthorized request is rejected
- **WHEN** a request to an admin endpoint lacks a valid token (or comes from a non-allowlisted IP)
- **THEN** the response denies access (401/403) and no state is returned

### Requirement: Observability
The system SHALL emit structured logs and expose a metrics endpoint (`/metrics`) with operational metrics
(e.g. active routes, requests, certificate status).

#### Scenario: Metrics endpoint is scrapable
- **WHEN** a client issues `GET /metrics`
- **THEN** the response is a metrics exposition that a Prometheus-compatible scraper can parse
