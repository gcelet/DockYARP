## MODIFIED Requirements

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, `X-Real-IP`,
`X-Forwarded-Ssl` (`on` when the effective forwarded proto is HTTPS, else `off`), and `X-Original-URI` (the
original request URI: path base + path + query), and SHALL forward an appropriate `Host` header.
`X-Forwarded-Ssl` SHALL mirror `X-Forwarded-Proto` (respecting downstream-proxy trust); `X-Original-URI` SHALL
always be the real original URI (a client-supplied value is replaced).

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

#### Scenario: SSL and original-URI headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-Ssl` (`on`/`off` matching the forwarded proto) and `X-Original-URI`
  carrying the original request path and query
