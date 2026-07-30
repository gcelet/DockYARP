## MODIFIED Requirements

### Requirement: Default fallback certificate
The system SHALL present a default certificate for handshakes targeting a host without a specific certificate,
so the connection is handled deterministically rather than aborting. When an operator-supplied `default.crt`
and `default.key` exist in the certificate directory, the system SHALL present that certificate as the
fallback; otherwise it SHALL present a generated self-signed certificate. The `default` basename SHALL be
reserved for this fallback and SHALL NOT be registered as a per-host certificate.

#### Scenario: Handshake for an unknown host
- **WHEN** a TLS handshake targets a host that has no stored certificate
- **THEN** the selector returns the default fallback certificate

#### Scenario: Operator-supplied default certificate is preferred
- **WHEN** `default.crt` and `default.key` are present in the certificate directory
- **THEN** the fallback presented for unknown hosts is the operator certificate, not the self-signed one

#### Scenario: Self-signed default when none is supplied
- **WHEN** no `default.crt`/`default.key` is present
- **THEN** the fallback presented for unknown hosts is the generated self-signed certificate
