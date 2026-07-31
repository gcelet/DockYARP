## MODIFIED Requirements

### Requirement: Access logging
The system SHALL emit a structured access-log entry for each handled request, including the request method,
host, path, response status, and elapsed time, unless access logging is disabled or the request path starts
with a configured excluded prefix (for infrastructure endpoints such as `/metrics` and `/api`). The rendered
format (text or JSON) follows the configured logging provider. The system SHALL support an operator-defined
field selection (`AccessLog:Fields`) from a fixed catalog; when configured, each entry SHALL contain exactly
those fields in the configured order, and when not configured the default fields SHALL be emitted unchanged.

#### Scenario: Request is logged
- **WHEN** access logging is enabled and a request is handled
- **THEN** a structured access-log entry with the method, path, response status, and elapsed time is emitted

#### Scenario: Logging can be disabled
- **WHEN** access logging is disabled
- **THEN** no access-log entry is emitted for a handled request

#### Scenario: Infrastructure paths are excluded
- **WHEN** a request targets a path under a configured excluded prefix (for example `/metrics`)
- **THEN** no access-log entry is emitted for it

#### Scenario: Custom field selection is honored
- **WHEN** `AccessLog:Fields` lists a specific set of fields
- **THEN** each access-log entry contains exactly those fields, in the configured order

#### Scenario: Default fields when unconfigured
- **WHEN** no `AccessLog:Fields` is configured
- **THEN** the default access-log fields are emitted unchanged
