## ADDED Requirements

### Requirement: Static configuration source
The system SHALL load routes and clusters from a static configuration file when one is configured, as a
`Static` configuration contribution, and SHALL merge it with dynamic (discovery) contributions using the
existing precedence (static wins on conflicts). When no file is configured, the static contribution SHALL be
empty. Static configuration SHALL be applied whether or not Docker discovery is enabled.

#### Scenario: Static routes and clusters are loaded
- **WHEN** a static configuration file declares a cluster `api` with an address and a route `api.local` → `api`
- **THEN** the routing store serves a route for `api.local` targeting that cluster

#### Scenario: Static configuration wins over discovery
- **WHEN** both the static file and Docker discovery define a route for the same host and path
- **THEN** the static definition is the one applied

#### Scenario: No file yields an empty contribution
- **WHEN** no static configuration file is configured
- **THEN** the static contribution is empty and only discovered routes (if any) are served
