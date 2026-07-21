## ADDED Requirements

### Requirement: Route authentication metadata
The routing model SHALL allow a route to carry optional Basic Auth credentials (username, password, and an
optional realm) so the security capability can protect it. The routing model only stores this metadata; it
performs no authentication itself.

#### Scenario: Route carries Basic Auth credentials
- **WHEN** a route is configured with a username and password
- **THEN** the route exposes those credentials (and optional realm) for the security layer to enforce

#### Scenario: Route without credentials is unprotected
- **WHEN** a route is configured without auth credentials
- **THEN** the route exposes no credentials and is not protected
