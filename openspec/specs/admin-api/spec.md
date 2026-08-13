# admin-api Specification

## Purpose
TBD - created by archiving change add-admin-api. Update Purpose after archive.
## Requirements
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

### Requirement: Certificate reporting
The `/api/certs` endpoint SHALL return the certificates currently in the certificate store (host and
expiry), without exposing private keys.

#### Scenario: Stored certificates are listed
- **WHEN** a certificate for `app.local` is in the store and an authorized client issues `GET /api/certs`
- **THEN** the response includes `app.local` with its expiry and no private key material

#### Scenario: Empty store returns an empty list
- **WHEN** no certificates are stored
- **THEN** `GET /api/certs` returns an empty list

### Requirement: Real health reporting
The `/api/health` endpoint SHALL report a status derived from real signals — Docker discovery connectivity
(when enabled), stored certificate count, and active route/cluster counts — and SHALL degrade the overall
status when a monitored dependency is unavailable.

#### Scenario: Healthy when dependencies are up
- **WHEN** discovery is connected (or disabled) and the store is populated
- **THEN** `GET /api/health` reports a healthy status with the real counts

#### Scenario: Degraded when a dependency is down
- **WHEN** Docker discovery is enabled but cannot reach the daemon
- **THEN** `GET /api/health` reports a degraded/unhealthy status

### Requirement: Access logging
The system SHALL emit a structured access-log entry for each handled request, including the request method,
host, path, response status, and elapsed time, unless access logging is disabled or the request path starts
with a configured excluded prefix (for infrastructure endpoints such as `/metrics` and `/api`). The rendered
format (text or JSON) follows the configured logging provider. The system SHALL support an operator-defined
field selection (`AccessLog:Fields`) from a fixed catalog; when configured, each entry SHALL contain exactly
those fields in the configured order, and when not configured the default fields SHALL be emitted unchanged.

#### Scenario: Request is logged
- **WHEN** access logging is enabled and a request is handled
- **THEN** a structured access-log entry with the method, path, response status, and elapsed time is emitted

#### Scenario: Logging can be disabled
- **WHEN** access logging is disabled
- **THEN** no access-log entry is emitted for a handled request

#### Scenario: Infrastructure paths are excluded
- **WHEN** a request targets a path under a configured excluded prefix (for example `/metrics`)
- **THEN** no access-log entry is emitted for it

#### Scenario: Custom field selection is honored
- **WHEN** `AccessLog:Fields` lists a specific set of fields
- **THEN** each access-log entry contains exactly those fields, in the configured order

#### Scenario: Default fields when unconfigured
- **WHEN** no `AccessLog:Fields` is configured
- **THEN** the default access-log fields are emitted unchanged

### Requirement: Configuration resolution endpoint
The admin API SHALL expose an authenticated endpoint that resolves a host and path to the effective
configuration, using the same route resolution as the request pipeline. The response SHALL include the matched
route, its transforms, TLS metadata, security policy, and target cluster, as sanitized JSON (no secrets). The
endpoint SHALL be protected by the admin API key like the other admin endpoints.

#### Scenario: Resolve returns the effective configuration
- **WHEN** an authenticated operator requests `/api/resolve` for a host and path that match a route
- **THEN** the response is JSON describing the matched route, transforms, TLS, security, and cluster

#### Scenario: No matching route
- **WHEN** an authenticated operator resolves a host/path that matches no route
- **THEN** the response indicates no match (404)

#### Scenario: Unauthenticated resolve is rejected
- **WHEN** the resolve endpoint is requested without a valid API key
- **THEN** the request is rejected with 401

### Requirement: Admin endpoint host isolation
The system SHALL support scoping the admin endpoints — the `/api/*` admin API and the `/metrics` endpoint — to a
dedicated host via the `AdminApi:Host` setting. When it is set, the admin endpoints SHALL respond only to requests
whose `Host` matches it; on every other host those paths SHALL fall through to normal reverse proxying, so a backend
that exposes an admin path (for example `/api/health` or `/metrics`) is not shadowed. When `AdminApi:Host` is unset,
the admin endpoints SHALL remain available on all hosts (backward compatible).

#### Scenario: Admin paths on other hosts are proxied, not shadowed
- **WHEN** `AdminApi:Host` is set and a request for a different host targets an admin path (e.g. `/api/health`)
- **THEN** the admin endpoint does not handle it, and the request is proxied (or falls to the default response) —
  not answered with the admin `401`

#### Scenario: Admin paths served on the admin host
- **WHEN** a request for the configured admin host targets an admin path
- **THEN** the admin endpoint handles it, behind the API-key protection

#### Scenario: Unset host keeps all-hosts behavior
- **WHEN** `AdminApi:Host` is unset
- **THEN** the admin endpoints respond on all hosts (backward compatible)
