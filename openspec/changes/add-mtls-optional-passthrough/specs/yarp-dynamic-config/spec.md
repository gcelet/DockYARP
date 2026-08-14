## MODIFIED Requirements

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, `X-Real-IP`,
`X-Forwarded-Ssl` (`on` when the effective forwarded proto is HTTPS, else `off`), and `X-Original-URI` (the
original request URI: path base + path + query), and SHALL forward an appropriate `Host` header.
`X-Forwarded-Ssl` SHALL mirror `X-Forwarded-Proto` (respecting downstream-proxy trust); `X-Original-URI` SHALL
always be the real original URI (a client-supplied value is replaced).

When a client certificate is present on the connection (verified mutual TLS), the system SHALL forward the client's
identity to the backend: `X-SSL-Client-Verify: SUCCESS`, `X-SSL-Client-S-DN` (the certificate subject), and
`X-SSL-Client-I-DN` (the certificate issuer). The system SHALL always strip any client-supplied `X-SSL-Client-*`
headers first, so the backend only ever receives DockYarp-set values; when no client certificate is present, no
`X-SSL-Client-*` header SHALL reach the backend.

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

#### Scenario: SSL and original-URI headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-Ssl` (`on`/`off` matching the forwarded proto) and `X-Original-URI`
  carrying the original request path and query

#### Scenario: Verified client certificate is forwarded
- **WHEN** a request is proxied over a connection carrying a verified client certificate
- **THEN** the backend receives `X-SSL-Client-Verify: SUCCESS` with the certificate's subject and issuer

#### Scenario: Client-supplied client-cert headers are stripped
- **WHEN** a client sends its own `X-SSL-Client-Verify`/`X-SSL-Client-S-DN` header and the connection has no verified
  client certificate
- **THEN** those headers are stripped and do not reach the backend
