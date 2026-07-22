## MODIFIED Requirements

### Requirement: HTTPS enforcement
The system SHALL redirect an HTTP request to HTTPS when the matched route's TLS metadata selects a
redirecting HTTPS method (`redirect` or `nohttp`) **and** a certificate is available for the host,
preserving the host and path. A `noredirect` or `nohttps` method SHALL never redirect, and a host with no
available certificate SHALL be served over HTTP without a redirect.

#### Scenario: HTTP redirected to HTTPS when a certificate is available
- **WHEN** an HTTP request targets a host whose route selects `redirect` and a certificate is available for the host
- **THEN** the response redirects to the HTTPS URL for the same host and path

#### Scenario: No redirect until a certificate is available
- **WHEN** an HTTP request targets a host whose route selects `redirect` but no certificate is available yet
- **THEN** the request is served over HTTP without a redirect

#### Scenario: No redirect for a non-redirecting method
- **WHEN** an HTTP request targets a host whose route selects `noredirect`
- **THEN** the request is served over HTTP without a redirect
