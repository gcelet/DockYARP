## ADDED Requirements

### Requirement: Per-host Server header (SERVER_TOKENS)
The system SHALL recognize a per-container `SERVER_TOKENS` value (environment variable or label, with the
environment variable taking precedence). When a host declares `SERVER_TOKENS=off` (or an empty value), the
system SHALL NOT emit the `Server` response header for that host, overriding any globally-configured `Server`
value. Any other value, or no value, SHALL leave the global `Server` behavior unchanged for the host.

#### Scenario: Per-host off suppresses the Server header
- **WHEN** a global `Server` header value is configured and a host declares `SERVER_TOKENS=off`
- **THEN** responses for that host carry no `Server` header, while other hosts still emit the global value

#### Scenario: No per-host value keeps the global behavior
- **WHEN** a host declares no `SERVER_TOKENS`
- **THEN** the global `Server` header behavior applies to that host
