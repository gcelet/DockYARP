## MODIFIED Requirements

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, `X-Real-IP`,
`X-Forwarded-Ssl` (`on` when the effective forwarded proto is HTTPS, else `off`), and `X-Original-URI` (the
original request URI: path base + path + query), and SHALL forward an appropriate `Host` header.
`X-Forwarded-Ssl` SHALL mirror `X-Forwarded-Proto` (respecting downstream-proxy trust); `X-Original-URI` SHALL
always be the real original URI (a client-supplied value is replaced).

For a route with a `required` or `optional` client-certificate requirement, the system SHALL forward the
connection's client-certificate verification outcome to the backend as `X-SSL-Client-Verify`: `SUCCESS` when a
presented certificate chains to the configured CA and is not revoked, `FAILED` when a certificate was presented
but does not chain to the CA or is revoked, or `NONE` when no certificate was presented. `X-SSL-Client-S-DN`
(the certificate subject) and `X-SSL-Client-I-DN` (the certificate issuer) SHALL be forwarded only alongside
`SUCCESS`. For a route with no client-certificate requirement, no `X-SSL-Client-*` header SHALL reach the
backend. The system SHALL always strip any client-supplied `X-SSL-Client-*` headers first, so the backend only
ever receives DockYarp-set values.

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

#### Scenario: SSL and original-URI headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-Ssl` (`on`/`off` matching the forwarded proto) and `X-Original-URI`
  carrying the original request path and query

#### Scenario: Verified client certificate is forwarded
- **WHEN** a request is proxied over a connection carrying a verified client certificate on a `required` or
  `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: SUCCESS` with the certificate's subject and issuer

#### Scenario: Untrusted or revoked client certificate is reported as FAILED
- **WHEN** a request is proxied over a connection carrying a client certificate that does not chain to the
  configured CA, or is revoked, on an `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: FAILED` with no subject/issuer headers, and the request is
  not rejected

#### Scenario: Missing client certificate is reported as NONE
- **WHEN** a request is proxied over a connection with no client certificate on an `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: NONE` with no subject/issuer headers

#### Scenario: Route without a client-certificate requirement gets no SSL-client headers
- **WHEN** a request is proxied to a route with no client-certificate requirement
- **THEN** the backend receives no `X-SSL-Client-*` header

#### Scenario: Client-supplied client-cert headers are stripped
- **WHEN** a client sends its own `X-SSL-Client-Verify`/`X-SSL-Client-S-DN` header
- **THEN** those headers are stripped from the proxied request before any DockYarp-set value is applied
