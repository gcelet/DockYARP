# tls-acme Specification

## Purpose
TBD - created by archiving change add-tls-acme. Update Purpose after archive.
## Requirements
### Requirement: ACME certificate acquisition
The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge, and SHALL store the resulting certificate in the certificate store.

#### Scenario: Certificate obtained for a labeled host
- **WHEN** a host declares TLS metadata (from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`) and no current certificate exists
- **THEN** the provisioning service requests a certificate for that host and stores it

#### Scenario: HTTP-01 challenge is answered
- **WHEN** the ACME provider requests validation of a token
- **THEN** the challenge is served at `/.well-known/acme-challenge/{token}` with the expected key authorization

### Requirement: Certificate renewal
The system SHALL renew a stored certificate before it expires, using a configurable renewal margin, without
operator intervention.

#### Scenario: Certificate near expiry is renewed
- **WHEN** a stored certificate is within the configured renewal margin of expiry
- **THEN** the provisioning service requests a new certificate and replaces the stored one

### Requirement: Kestrel SNI with hot reload
The system SHALL select the certificate matching the requested host via SNI, and SHALL serve a newly
stored certificate without a process restart.

#### Scenario: Certificate served by host
- **WHEN** a certificate for `app.local` is present in the store and a TLS handshake targets `app.local`
- **THEN** the selector returns that certificate

#### Scenario: Newly stored certificate is used without restart
- **WHEN** a certificate for a new host is stored at runtime
- **THEN** the selector returns it for that host on the next handshake

### Requirement: Default fallback certificate
The system SHALL present a default self-signed certificate for handshakes targeting a host without a
specific certificate, so the connection is handled deterministically rather than aborting.

#### Scenario: Handshake for an unknown host
- **WHEN** a TLS handshake targets a host that has no stored certificate
- **THEN** the selector returns the default fallback certificate

