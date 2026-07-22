## ADDED Requirements

### Requirement: Client certificate CA validation
When a client CA certificate is configured, the system SHALL make the HTTPS endpoint request a client
certificate and SHALL accept a presented certificate only when it chains to the configured CA; a
certificate that does not chain to the CA SHALL be rejected. When no client CA is configured, no client
certificate is requested.

#### Scenario: Certificate chaining to the CA is accepted
- **WHEN** a client certificate signed by the configured CA is validated
- **THEN** validation succeeds

#### Scenario: Certificate not chaining to the CA is rejected
- **WHEN** a client certificate not signed by the configured CA is validated
- **THEN** validation fails
