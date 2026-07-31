## ADDED Requirements

### Requirement: Per-connection TLS session assembly
The system SHALL assemble the TLS session for each HTTPS connection — the server certificate, the enabled TLS
protocols, the cipher policy, and the client-certificate policy — from the connection's SNI host at handshake
time, defaulting to the globally-configured TLS posture when the host declares no override. The plaintext HTTP
endpoint SHALL be served over HTTP/1.1 without TLS. Data-plane endpoints SHALL be bound from the configured
HTTP and HTTPS ports.

#### Scenario: Per-connection assembly preserves the global posture
- **WHEN** a TLS connection targets a host that declares no TLS override
- **THEN** the connection presents the certificate selected by SNI, the globally-configured minimum TLS
  protocol version and cipher policy, and the globally-configured client-certificate behavior

#### Scenario: Mutual TLS is preserved through the callback
- **WHEN** a client CA is configured and a client presents a certificate chaining to that CA
- **THEN** the connection is accepted, and a certificate that does not chain to the CA is rejected

#### Scenario: The HTTP endpoint stays plaintext HTTP/1.1
- **WHEN** a request reaches the HTTP endpoint (ACME challenge or redirect)
- **THEN** it is served over HTTP/1.1 without TLS
