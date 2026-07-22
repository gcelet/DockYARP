## ADDED Requirements

### Requirement: Client certificate label
The system SHALL read `DOCKYARP_CLIENT_CERT` from a container's labels and set the route's
client-certificate requirement (`required`, `optional`, or `none`/`off`), defaulting to `none` when absent
and falling back to `none` (with a warning) for an unrecognized value.

#### Scenario: Client certificate label sets the requirement
- **WHEN** a container declares `DOCKYARP_CLIENT_CERT=required`
- **THEN** the mapped route requires a client certificate

#### Scenario: Unrecognized value falls back to none
- **WHEN** a container declares `DOCKYARP_CLIENT_CERT=maybe`
- **THEN** the route requires no client certificate and the invalid value is reported
