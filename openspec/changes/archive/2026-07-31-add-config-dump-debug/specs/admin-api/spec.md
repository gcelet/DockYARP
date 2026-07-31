## ADDED Requirements

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
