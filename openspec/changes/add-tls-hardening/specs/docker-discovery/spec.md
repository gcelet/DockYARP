## ADDED Requirements

### Requirement: HSTS label
The system SHALL read an `HSTS` label and carry it as the route's per-host HSTS policy on its TLS metadata:
a value sets the `Strict-Transport-Security` header for the host, and `off` disables HSTS for the host. When
absent, the global HSTS policy applies.

#### Scenario: HSTS label sets a per-host policy
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HSTS=off`
- **THEN** the mapped route's TLS metadata carries the `off` HSTS policy
