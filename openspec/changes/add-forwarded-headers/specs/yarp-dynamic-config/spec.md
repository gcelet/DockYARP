## ADDED Requirements

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, and `X-Real-IP`,
and SHALL forward an appropriate `Host` header.

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

### Requirement: Downstream proxy trust
The system SHALL provide a configurable trust setting that determines whether client-supplied
`X-Forwarded-*` headers are preserved/appended (trusted) or replaced with the connection's own scheme,
host, and port (untrusted).

#### Scenario: Untrusted mode overrides client headers
- **WHEN** downstream proxy trust is disabled and a client sends its own `X-Forwarded-Proto`
- **THEN** the proxied request's forwarded values reflect the actual connection, not the client-supplied header

#### Scenario: Trusted mode preserves upstream headers
- **WHEN** downstream proxy trust is enabled and a trusted upstream sets `X-Forwarded-For`
- **THEN** the proxied request appends to, rather than discards, the existing forwarded chain
