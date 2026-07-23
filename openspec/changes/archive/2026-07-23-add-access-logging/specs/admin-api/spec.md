## ADDED Requirements

### Requirement: Access logging
The system SHALL emit a structured access-log entry for each handled request, including the request method,
host, path, response status, and elapsed time, unless access logging is disabled. The rendered format
(text or JSON) follows the configured logging provider.

#### Scenario: Request is logged
- **WHEN** access logging is enabled and a request is handled
- **THEN** a structured access-log entry with the method, path, response status, and elapsed time is emitted

#### Scenario: Logging can be disabled
- **WHEN** access logging is disabled
- **THEN** no access-log entry is emitted for a handled request
