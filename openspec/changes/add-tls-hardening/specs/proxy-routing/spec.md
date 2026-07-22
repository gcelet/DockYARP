## MODIFIED Requirements

### Requirement: Per-host TLS metadata
The system SHALL allow a route/host to carry TLS metadata (certificate host name, contact email, an HTTPS
method controlling HTTP↔HTTPS behavior, and an optional per-host HSTS policy) so that downstream TLS and
security capabilities can consume it without re-parsing labels.

#### Scenario: Host flagged for a certificate
- **WHEN** a route for host `app.local` declares certificate host `app.local` and email `admin@example.com`
- **THEN** the model exposes that host as requiring a certificate for `app.local` with that contact email

#### Scenario: Host carries an HTTPS method
- **WHEN** a route's TLS metadata sets the HTTPS method to `noredirect`
- **THEN** the model exposes that method for the security layer to apply

#### Scenario: Host carries an HSTS policy
- **WHEN** a route's TLS metadata sets a per-host HSTS policy
- **THEN** the model exposes that policy for the security layer to apply
