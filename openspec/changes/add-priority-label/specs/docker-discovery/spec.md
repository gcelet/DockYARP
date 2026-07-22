## ADDED Requirements

### Requirement: Priority label
The system SHALL read `DOCKYARP_PRIORITY` from a container's labels and use it as the route's priority,
defaulting to `0` when absent and falling back to `0` (with a warning) for a non-numeric value.

#### Scenario: Priority label sets the route priority
- **WHEN** a container declares `DOCKYARP_PRIORITY=10`
- **THEN** the mapped route has priority `10`

#### Scenario: Non-numeric priority falls back to zero
- **WHEN** a container declares `DOCKYARP_PRIORITY=high`
- **THEN** the mapped route has priority `0` and the invalid value is reported
