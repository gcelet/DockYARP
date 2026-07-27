## ADDED Requirements

### Requirement: Server response header control
The system SHALL suppress the `Server` response header by default, and SHALL emit a configured value instead
when one is provided, so it does not disclose the underlying server technology.

#### Scenario: Server header suppressed by default
- **WHEN** a response is returned and no `Server` header value is configured
- **THEN** the response contains no `Server` header

#### Scenario: Configured Server header value
- **WHEN** a `Server` header value is configured
- **AND** a response is returned
- **THEN** the response `Server` header equals the configured value
