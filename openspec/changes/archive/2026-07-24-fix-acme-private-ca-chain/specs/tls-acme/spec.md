## MODIFIED Requirements

### Requirement: ACME certificate acquisition
The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge, and SHALL store the resulting certificate in the certificate store. Acquisition SHALL
succeed whether or not the ACME provider's issued chain includes the CA root: when the full chain cannot be
completed to a root (as with a private or custom CA), the system SHALL fall back to storing the issued leaf
certificate rather than failing provisioning.

#### Scenario: Certificate obtained for a labeled host
- **WHEN** a host declares TLS metadata (from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`) and no current certificate exists
- **THEN** the provisioning service requests a certificate for that host and stores it

#### Scenario: HTTP-01 challenge is answered
- **WHEN** the ACME provider requests validation of a token
- **THEN** the challenge is served at `/.well-known/acme-challenge/{token}` with the expected key authorization

#### Scenario: Private CA whose root is not in the issued chain
- **WHEN** the ACME provider issues a certificate but its chain does not include the CA root (a private or
  custom CA)
- **THEN** the system still stores a usable certificate for the host (falling back to the issued leaf) instead
  of failing to provision
