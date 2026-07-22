## ADDED Requirements

### Requirement: Default host selection
The system SHALL support designating a default host whose route also matches requests whose host matches no
other route, so a chosen backend serves unknown hosts. When no default host is configured, an unmatched
host yields no route.

#### Scenario: Default host serves unknown hosts
- **WHEN** a default host `app.local` is configured and a request arrives for `unknown.example`
- **THEN** the request is matched to the default host's route

#### Scenario: No default host means no match
- **WHEN** no default host is configured and a request arrives for an unknown host
- **THEN** matching yields no route
