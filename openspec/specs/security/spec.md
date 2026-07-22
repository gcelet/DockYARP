# security Specification

## Purpose
TBD - created by archiving change add-security-middleware. Update Purpose after archive.
## Requirements
### Requirement: HTTPS enforcement
The system SHALL redirect an HTTP request to HTTPS when the matched route's TLS metadata selects a
redirecting HTTPS method (`redirect` or `nohttp`) **and** a certificate is available for the host,
preserving the host and path. A `noredirect` or `nohttps` method SHALL never redirect, and a host with no
available certificate SHALL be served over HTTP without a redirect.

#### Scenario: HTTP redirected to HTTPS for an enforced host
- **WHEN** an HTTP request targets a host whose route selects `redirect` and a certificate is available for the host
- **THEN** the response redirects to the HTTPS URL for the same host and path

#### Scenario: No redirect when enforcement is off
- **WHEN** an HTTP request targets a host whose route selects `noredirect`
- **THEN** the request is served over HTTP without a redirect

#### Scenario: No redirect until a certificate is available
- **WHEN** an HTTP request targets a host whose route selects `redirect` but no certificate is available yet
- **THEN** the request is served over HTTP without a redirect

### Requirement: Route Basic Auth
The system SHALL protect a route that carries Basic Auth credentials, rejecting requests with missing or
invalid credentials with 401 and a `WWW-Authenticate: Basic` challenge, and never logging credentials in
clear text.

#### Scenario: Missing credentials are rejected
- **WHEN** a request to a protected route has no valid `Authorization` header
- **THEN** the response status is 401 with a `WWW-Authenticate: Basic` header

#### Scenario: Valid credentials are accepted
- **WHEN** a request to a protected route presents the configured username and password
- **THEN** the request proceeds to the reverse proxy

#### Scenario: Unprotected route is not challenged
- **WHEN** a request targets a route without auth credentials
- **THEN** no authentication is required

### Requirement: Security headers
The system SHALL apply configurable security headers to proxied responses, including HSTS on HTTPS
responses and `X-Content-Type-Options`, `X-Frame-Options`, and `Referrer-Policy`.

#### Scenario: HSTS applied over HTTPS
- **WHEN** a response is served over HTTPS
- **THEN** it includes a `Strict-Transport-Security` header

#### Scenario: Baseline headers applied
- **WHEN** any response is produced
- **THEN** it includes `X-Content-Type-Options: nosniff`

