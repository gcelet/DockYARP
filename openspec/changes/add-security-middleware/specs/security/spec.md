## ADDED Requirements

### Requirement: HTTPS enforcement
The system SHALL redirect HTTP requests to HTTPS for hosts where a certificate is available, with the
redirect behavior configurable per host.

#### Scenario: HTTP redirected to HTTPS
- **WHEN** an HTTP request targets a host that has a certificate and enforcement is enabled
- **THEN** the request is redirected to the HTTPS URL for the same host and path

#### Scenario: Enforcement disabled for a host
- **WHEN** enforcement is disabled for a host
- **THEN** HTTP requests to that host are served without a redirect

### Requirement: Label-driven Basic Auth
The system SHALL protect routes with Basic Auth when configured via labels, returning 401 for missing or
invalid credentials and never logging credentials in clear text.

#### Scenario: Missing credentials are rejected
- **WHEN** a request to a Basic-Auth-protected route has no valid `Authorization` header
- **THEN** the response status is 401 and access is denied

#### Scenario: Valid credentials are accepted
- **WHEN** a request to a protected route presents valid credentials
- **THEN** the request is proxied to the backend

### Requirement: Security headers
The system SHALL apply HSTS and common security headers to proxied responses, with the header set
configurable.

#### Scenario: HSTS applied over HTTPS
- **WHEN** a response is served over HTTPS for an enforced host
- **THEN** the response includes a `Strict-Transport-Security` header
