## ADDED Requirements

### Requirement: Default response for unmatched requests
The system SHALL return a configurable default response for requests that match no route and no default
host — for example a status code (`404`, `503`) or a redirect — instead of a bare not-found.

#### Scenario: Configured default status
- **WHEN** the default response is configured as `503` and a request matches no route
- **THEN** the response status is 503

#### Scenario: Default is 404 when unset
- **WHEN** no default response is configured and a request matches no route
- **THEN** the response status is 404
