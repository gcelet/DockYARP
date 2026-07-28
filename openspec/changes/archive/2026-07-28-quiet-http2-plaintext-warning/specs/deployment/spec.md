## ADDED Requirements

### Requirement: Plaintext HTTP endpoint protocol
The plaintext HTTP endpoint (which serves ACME HTTP-01 challenges and HTTP→HTTPS redirects) SHALL negotiate
HTTP/1.1 only — HTTP/2 requires TLS — while the HTTPS endpoint retains its configured protocols (HTTP/1.1 and
HTTP/2). This avoids Kestrel's spurious "HTTP/2 is not enabled … TLS is not enabled" startup warning.

#### Scenario: HTTP endpoint is HTTP/1.1 only
- **WHEN** DockYarp starts with a plaintext HTTP endpoint and a TLS HTTPS endpoint
- **THEN** the HTTP endpoint is configured for HTTP/1.1 only and no HTTP/2-without-TLS warning is emitted

#### Scenario: HTTPS endpoint keeps HTTP/2
- **WHEN** the HTTPS endpoint is configured
- **THEN** it retains the configured protocols (HTTP/1.1 and HTTP/2)
