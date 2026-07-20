## ADDED Requirements

### Requirement: ACME certificate acquisition
The system SHALL obtain certificates from an ACME v2 provider using the HTTP-01 challenge by default for
hosts that declare TLS metadata, and SHALL store them under the configured certificate directory.

#### Scenario: Certificate obtained for a labeled host
- **WHEN** a host declares `LETSENCRYPT_HOST=app.local` and `LETSENCRYPT_EMAIL=admin@example.com`
- **THEN** a valid certificate for `app.local` is obtained and stored

### Requirement: Certificate renewal
The system SHALL renew certificates before expiry via a background scheduler using a configurable renewal
margin, without operator intervention.

#### Scenario: Certificate near expiry is renewed
- **WHEN** a stored certificate is within the configured renewal margin of expiry
- **THEN** the scheduler renews it and replaces the stored certificate

### Requirement: Kestrel SNI integration with hot reload
The system SHALL serve the correct certificate per host via SNI and SHALL pick up newly added or renewed
certificates without a process restart.

#### Scenario: New certificate becomes available
- **WHEN** a certificate for a new host is added to the certificate store
- **THEN** HTTPS for that host works without restarting DockYarp

### Requirement: Default fallback certificate
The system SHALL present a default certificate for TLS handshakes targeting hosts without a specific
certificate, so the handshake fails gracefully rather than aborting unexpectedly.

#### Scenario: Handshake for an unknown host
- **WHEN** a TLS handshake targets a host that has no specific certificate
- **THEN** the default certificate is presented and the connection is handled deterministically
