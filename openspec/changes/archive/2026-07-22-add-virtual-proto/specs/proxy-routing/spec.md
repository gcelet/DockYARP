## ADDED Requirements

### Requirement: Backend scheme on endpoints
The routing model SHALL allow a cluster endpoint to carry a backend scheme (at least `http` and `https`),
defaulting to `http`, so downstream proxying can target HTTPS backends.

#### Scenario: Endpoint exposes its scheme
- **WHEN** an endpoint is created with scheme `https`
- **THEN** the endpoint's address targets the backend over HTTPS

#### Scenario: Default scheme is http
- **WHEN** an endpoint is created without an explicit scheme
- **THEN** the endpoint targets the backend over HTTP
