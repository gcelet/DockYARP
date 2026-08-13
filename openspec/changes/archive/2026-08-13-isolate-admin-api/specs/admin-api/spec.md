## ADDED Requirements

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
