# security Specification

## Purpose
TBD - created by archiving change add-security-middleware. Update Purpose after archive.
## Requirements
### Requirement: HTTPS enforcement
The system SHALL redirect an HTTP request to HTTPS when the matched route's TLS metadata selects a
redirecting HTTPS method (`redirect` or `nohttp`) **and** a certificate is available for the host,
preserving the host and path. The redirect SHALL use status 308 (permanent, method-preserving) so that a
non-GET request keeps its method and body on replay. A `noredirect` or `nohttps` method SHALL never redirect,
and a host with no available certificate SHALL be served over HTTP without a redirect.

#### Scenario: HTTP redirected to HTTPS for an enforced host
- **WHEN** an HTTP request targets a host whose route selects `redirect` and a certificate is available for the host
- **THEN** the response redirects to the HTTPS URL for the same host and path

#### Scenario: No redirect when enforcement is off
- **WHEN** an HTTP request targets a host whose route selects `noredirect`
- **THEN** the request is served over HTTP without a redirect

#### Scenario: No redirect until a certificate is available
- **WHEN** an HTTP request targets a host whose route selects `redirect` but no certificate is available yet
- **THEN** the request is served over HTTP without a redirect

#### Scenario: Non-GET request keeps its method on redirect
- **WHEN** a non-GET HTTP request (e.g. POST) is redirected to HTTPS for an enforced host
- **THEN** the redirect status is 308 and the client repeats the request with the same method

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

### Requirement: HSTS preload and per-host override
The system SHALL support an HSTS `preload` directive on the global policy and a per-host HSTS override
carried on the route's TLS metadata: a per-host value replaces the emitted `Strict-Transport-Security`
header, and `off` suppresses HSTS for that host. HSTS is only emitted on HTTPS responses.

#### Scenario: Preload directive is emitted
- **WHEN** HSTS preload is enabled and an HTTPS response is produced
- **THEN** the `Strict-Transport-Security` header includes `preload`

#### Scenario: Per-host override suppresses HSTS
- **WHEN** an HTTPS response is produced for a host whose route sets HSTS to `off`
- **THEN** no `Strict-Transport-Security` header is emitted for that response

### Requirement: Reject HTTPS for a nohttps host
The system SHALL refuse an HTTPS request whose matched route selects the `nohttps` method, since that host
is served over HTTP only.

#### Scenario: HTTPS request to a nohttps host is refused
- **WHEN** an HTTPS request targets a host whose route selects `nohttps`
- **THEN** the response status is 404 and the request is not proxied

### Requirement: Client certificate enforcement
The system SHALL reject a request to a route that requires a client certificate when no client certificate
was presented on the connection, responding with 403. Routes with a requirement of `optional` or `none`
SHALL NOT be rejected for a missing client certificate.

#### Scenario: Required route without a client certificate is rejected
- **WHEN** a request targets a route requiring a client certificate and the connection presents none
- **THEN** the response status is 403 and the request is not proxied

#### Scenario: Required route with a client certificate is served
- **WHEN** a request targets a route requiring a client certificate and the connection presents one
- **THEN** the request continues through the pipeline

#### Scenario: Route without a requirement is served
- **WHEN** a request targets a route with no client-certificate requirement and presents none
- **THEN** the request continues through the pipeline

### Requirement: Single route resolution per request
The request pipeline SHALL resolve the matched route at most once per request and reuse that result across
the middlewares that need it, rather than resolving it independently in each. The shared result SHALL be
stable for the lifetime of the request.

#### Scenario: The resolved route is cached for the request
- **WHEN** the route has been resolved for a request and the routing store subsequently changes
- **THEN** the same request continues to observe the route resolved on first access

### Requirement: Server response header control
The system SHALL suppress the `Server` response header by default, and SHALL emit a configured value instead
when one is provided, so it does not disclose the underlying server technology.

#### Scenario: Server header suppressed by default
- **WHEN** a response is returned and no `Server` header value is configured
- **THEN** the response contains no `Server` header

#### Scenario: Configured Server header value
- **WHEN** a `Server` header value is configured
- **AND** a response is returned
- **THEN** the response `Server` header equals the configured value

