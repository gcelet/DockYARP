## ADDED Requirements

### Requirement: Remote daemon TLS
When the Docker endpoint is a `tcp://` URL and a certificate directory (`Docker:CertPath`, holding `ca.pem`,
`cert.pem`, `key.pem`) is configured, the system SHALL connect to the daemon presenting the client certificate
(`cert.pem` + `key.pem`). When `Docker:TlsVerify` is `true`, it SHALL validate the daemon's certificate against
the configured CA (`ca.pem`) using custom root trust and reject a daemon whose certificate does not chain to it.
A unix-socket or named-pipe endpoint, or an endpoint with no `Docker:CertPath`, SHALL connect unchanged.

#### Scenario: TLS daemon connection uses the client certificate
- **WHEN** a `tcp://` endpoint is configured with a `Docker:CertPath` containing a client certificate
- **THEN** the daemon connection presents that client certificate

#### Scenario: Daemon certificate verified against the CA
- **WHEN** `Docker:TlsVerify` is `true` and the daemon presents a certificate chaining to the configured `ca.pem`
- **THEN** the connection is accepted; a daemon certificate that does not chain to the CA is rejected

#### Scenario: Socket endpoint unaffected
- **WHEN** the endpoint is a unix socket / named pipe, or no `Docker:CertPath` is set
- **THEN** the connection is established without client-certificate TLS, unchanged from before
