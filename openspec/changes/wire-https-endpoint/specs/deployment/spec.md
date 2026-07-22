## ADDED Requirements

### Requirement: HTTPS listener
The host SHALL listen for HTTPS on a configurable port and select the server certificate per request via
SNI using the certificate store, falling back to the default certificate for unknown hosts. HTTP SHALL
remain available for ACME HTTP-01 challenges and HTTP→HTTPS redirects.

#### Scenario: HTTPS served with the host certificate
- **WHEN** a certificate for `app.local` is present and a client connects over HTTPS with SNI `app.local`
- **THEN** the connection is served with that certificate

#### Scenario: Unknown host falls back to the default certificate
- **WHEN** a client connects over HTTPS for a host with no stored certificate
- **THEN** the default (self-signed) certificate is presented

#### Scenario: HTTP remains available
- **WHEN** the HTTPS listener is enabled
- **THEN** the host still accepts HTTP requests (for ACME challenges and redirects)

### Requirement: HTTPS port is exposed for containers
The container image and reference Compose stack SHALL expose and publish the HTTPS port so a deployed
instance is reachable over HTTPS.

#### Scenario: Reference stack exposes HTTPS
- **WHEN** the reference Compose stack is started
- **THEN** the HTTPS port is published and reachable
