## ADDED Requirements

### Requirement: Basic Auth labels
The system SHALL read `DOCKYARP_AUTH_USER`, `DOCKYARP_AUTH_PASSWORD`, and optional
`DOCKYARP_AUTH_REALM` from a container's labels and populate the route's Basic Auth credentials, so the
security layer can protect the route. Incomplete auth labels SHALL be logged and leave the route
unprotected without failing discovery.

#### Scenario: Auth labels protect the route
- **WHEN** a container declares `DOCKYARP_AUTH_USER=admin` and `DOCKYARP_AUTH_PASSWORD=secret`
- **THEN** the mapped route carries those Basic Auth credentials (with the realm if provided)

#### Scenario: Incomplete auth labels are ignored safely
- **WHEN** a container declares `DOCKYARP_AUTH_USER` but no `DOCKYARP_AUTH_PASSWORD`
- **THEN** the route is left unprotected and a warning is logged, and other containers are unaffected
