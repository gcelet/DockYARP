## MODIFIED Requirements

### Requirement: Client certificate CA validation
When a client CA certificate is configured, the system SHALL request a client certificate at the TLS handshake
for any host whose client-certificate requirement is `required` or `optional`; a host whose requirement is
`none` SHALL NOT be prompted for one. For a `required` host, the system SHALL accept a presented certificate
only when it chains to the configured CA and is not revoked per the configured CRL; a certificate that does not
chain to the CA, or that is revoked, SHALL cause the handshake to fail. For an `optional` host, the system SHALL
accept the handshake regardless of whether a presented certificate chains to the CA or is revoked, deferring the
verification outcome to the application layer. When no client CA is configured, no client certificate is
requested for any host.

#### Scenario: Certificate chaining to the CA is accepted
- **WHEN** a client certificate signed by the configured CA is validated
- **THEN** validation succeeds

#### Scenario: Certificate not chaining to the CA is rejected
- **WHEN** a client certificate not signed by the configured CA is validated
- **THEN** validation fails

#### Scenario: Revoked certificate is rejected
- **WHEN** a client certificate's serial number is listed in the configured CRL
- **THEN** validation fails, even if the certificate otherwise chains to the configured CA

#### Scenario: Required host fails the handshake for an untrusted certificate
- **WHEN** a client presents a certificate that does not chain to the configured CA to a host whose requirement
  is `required`
- **THEN** the TLS handshake fails and the connection is not established

#### Scenario: Optional host accepts the handshake despite an untrusted certificate
- **WHEN** a client presents a certificate that does not chain to the configured CA to a host whose requirement
  is `optional`
- **THEN** the TLS handshake succeeds and the connection is established

#### Scenario: Host with no client-certificate requirement is not prompted
- **WHEN** a client connects to a host whose client-certificate requirement is `none`
- **THEN** the TLS handshake does not request a client certificate

### Requirement: Per-connection TLS session assembly
The system SHALL assemble the TLS session for each HTTPS connection — the server certificate, the enabled TLS
protocols, the cipher policy, and the client-certificate policy — from the connection's SNI host at handshake
time, defaulting to the globally-configured TLS posture when the host declares no override. The
client-certificate policy SHALL be resolved per host from that host's `required`/`optional`/`none` requirement,
not applied uniformly across every host. The plaintext HTTP endpoint SHALL be served over HTTP/1.1 without TLS.
Data-plane endpoints SHALL be bound from the configured HTTP and HTTPS ports.

#### Scenario: Per-connection assembly preserves the global posture
- **WHEN** a TLS connection targets a host that declares no TLS override
- **THEN** the connection presents the certificate selected by SNI, the globally-configured minimum TLS
  protocol version and cipher policy, and the globally-configured client-certificate behavior

#### Scenario: Mutual TLS is preserved through the callback
- **WHEN** a client CA is configured and a client connects to a host whose requirement is `required`
- **THEN** the connection is accepted only when the presented certificate chains to that CA and is not revoked;
  otherwise the connection is rejected

#### Scenario: The HTTP endpoint stays plaintext HTTP/1.1
- **WHEN** a request reaches the HTTP endpoint (ACME challenge or redirect)
- **THEN** it is served over HTTP/1.1 without TLS
