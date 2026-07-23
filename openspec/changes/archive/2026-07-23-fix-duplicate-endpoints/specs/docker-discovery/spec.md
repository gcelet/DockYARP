## MODIFIED Requirements

### Requirement: Multiple hosts per container
The system SHALL accept a comma-separated `VIRTUAL_HOST` and map the container to one route per host, each
sharing the container's port, path, TLS, and auth settings. Empty entries SHALL be ignored, and a repeated
host SHALL be de-duplicated (case-insensitive).

#### Scenario: Comma-separated hosts create multiple routes
- **WHEN** a container declares `VIRTUAL_HOST=app.local,www.app.local`
- **THEN** routes are created for both `app.local` and `www.app.local` targeting the container

#### Scenario: Whitespace and empty entries are tolerated
- **WHEN** a container declares `VIRTUAL_HOST=a.local, ,b.local`
- **THEN** routes are created for `a.local` and `b.local` and the empty entry is ignored

#### Scenario: Repeated host is de-duplicated
- **WHEN** a container declares `VIRTUAL_HOST=app.local,app.local`
- **THEN** a single route/cluster is created for `app.local`
