## ADDED Requirements

### Requirement: Per-host HTTP/2 toggle
The system SHALL recognize a per-container HTTP/2 toggle — the `DOCKYARP_HTTP2` label or the nginx-proxy
`com.github.nginx-proxy.nginx-proxy.http2.enable` alias, a boolean, resolved from the container's merged
configuration — that controls whether HTTP/2 is offered to clients for a host. During the SNI handshake for a host
that sets the toggle to **false**, the system SHALL advertise only HTTP/1.1 via ALPN, overriding the global protocol
set; a host that leaves the toggle unset SHALL advertise the globally-configured protocols. Because HTTP/2 support is
bound at the HTTPS listener from the global protocol set, the toggle SHALL only **narrow** the offered protocols —
setting it to true has no effect unless HTTP/2 is enabled globally. The toggle is honored for TLS-configured hosts
(declaring `LETSENCRYPT_HOST` or `CERT_NAME`), consistent with the other per-host TLS attributes, and SHALL NOT
create an ACME certificate desire.

#### Scenario: Per-host disable narrows ALPN to HTTP/1.1
- **WHEN** a certified host disables HTTP/2 (`DOCKYARP_HTTP2=false`)
- **THEN** its handshake advertises only HTTP/1.1 via ALPN, while a host without the override keeps the global
  protocols

#### Scenario: Default host keeps the global protocols
- **WHEN** a host leaves the HTTP/2 toggle unset
- **THEN** its handshake advertises the globally-configured protocols (HTTP/2 offered by default)

#### Scenario: Enabling beyond the global set is a no-op
- **WHEN** a host sets the toggle to true while HTTP/2 is disabled globally
- **THEN** the host still negotiates HTTP/1.1 only (the listener does not offer HTTP/2)
