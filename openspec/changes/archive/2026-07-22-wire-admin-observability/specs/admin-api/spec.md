## ADDED Requirements

### Requirement: Certificate reporting
The `/api/certs` endpoint SHALL return the certificates currently in the certificate store (host and
expiry), without exposing private keys.

#### Scenario: Stored certificates are listed
- **WHEN** a certificate for `app.local` is in the store and an authorized client issues `GET /api/certs`
- **THEN** the response includes `app.local` with its expiry and no private key material

#### Scenario: Empty store returns an empty list
- **WHEN** no certificates are stored
- **THEN** `GET /api/certs` returns an empty list

### Requirement: Real health reporting
The `/api/health` endpoint SHALL report a status derived from real signals — Docker discovery connectivity
(when enabled), stored certificate count, and active route/cluster counts — and SHALL degrade the overall
status when a monitored dependency is unavailable.

#### Scenario: Healthy when dependencies are up
- **WHEN** discovery is connected (or disabled) and the store is populated
- **THEN** `GET /api/health` reports a healthy status with the real counts

#### Scenario: Degraded when a dependency is down
- **WHEN** Docker discovery is enabled but cannot reach the daemon
- **THEN** `GET /api/health` reports a degraded/unhealthy status
