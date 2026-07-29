## ADDED Requirements

### Requirement: Internal-only network access
The system SHALL allow a route to be restricted to internal client networks (`NETWORK_ACCESS=internal`). For
such a route, the system SHALL return HTTP 403 when the client IP is not within the configured set of internal
ranges, and SHALL serve the request when it is. The internal ranges SHALL be configurable, defaulting to the
common private ranges. The client IP SHALL be the request's direct connection address, and an address that
cannot be determined SHALL be treated as external (fail closed). Routes not marked internal-only SHALL be
unaffected.

#### Scenario: External client is denied
- **WHEN** a route is marked internal-only and a request arrives from a client IP outside the configured
  internal ranges
- **THEN** the response is 403 and the request is not proxied

#### Scenario: Internal client is served
- **WHEN** a route is marked internal-only and a request arrives from a client IP within the configured
  internal ranges
- **THEN** the request is allowed to proceed

#### Scenario: Custom internal ranges
- **WHEN** the internal ranges are customized in configuration and a request's client IP falls within the
  customized set
- **THEN** the request is allowed, and a client IP outside that set is denied

#### Scenario: Non-internal route is unaffected
- **WHEN** a route is not marked internal-only
- **THEN** requests from any client IP are allowed regardless of the internal ranges
