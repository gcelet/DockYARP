## ADDED Requirements

### Requirement: PROXY protocol edge listeners
When `Server:EnableProxyProtocol` is enabled, the system SHALL expect a PROXY protocol header (version 1 text
or version 2 binary) at the start of each edge connection, parse it, and set the connection's remote endpoint
to the client address it carries before HTTP processing, so forwarded headers reflect the real client. A
header that carries no client address (`UNKNOWN` in v1, `LOCAL`/`UNSPEC` in v2) SHALL leave the transport peer
unchanged, and a malformed header SHALL abort the connection. When the option is disabled, connections SHALL be
processed unchanged, expecting no header.

#### Scenario: Real client address recovered from the header
- **WHEN** the option is enabled and a connection begins with a valid PROXY protocol header for client `C`
- **THEN** the connection's remote endpoint is `C`, so `X-Forwarded-For`/`X-Real-IP` carry `C` and the HTTP
  request after the header is processed normally

#### Scenario: Address-less header leaves the peer unchanged
- **WHEN** the option is enabled and a connection begins with a `LOCAL`/`UNKNOWN` header (e.g. a balancer health check)
- **THEN** the connection's remote endpoint is left as the transport peer

#### Scenario: Disabled option is a no-op
- **WHEN** `Server:EnableProxyProtocol` is disabled
- **THEN** connections are processed exactly as before, with no PROXY header expected
