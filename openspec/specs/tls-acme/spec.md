# tls-acme Specification

## Purpose
TBD - created by archiving change add-tls-acme. Update Purpose after archive.
## Requirements
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

### Requirement: Provided certificate loading
The system SHALL load operator-provided certificates from the certificate directory at startup: PEM pairs
(`{host}.crt` with a matching `{host}.key`) and `{host}.pfx` files, keyed by the file name (the host). A
`.crt` without a matching `.key` SHALL be skipped. A provided certificate SHALL take precedence over an
ACME-persisted certificate for the same host.

#### Scenario: PEM pair is loaded
- **WHEN** `app.local.crt` and `app.local.key` are present in the certificate directory
- **THEN** the store serves a certificate for `app.local` with a usable private key

#### Scenario: PFX file is loaded
- **WHEN** `app.local.pfx` is present in the certificate directory
- **THEN** the store serves a certificate for `app.local`

#### Scenario: Unpaired certificate file is skipped
- **WHEN** `app.local.crt` is present without a matching `app.local.key`
- **THEN** no certificate is loaded for `app.local` from that file

### Requirement: Wildcard parent certificate selection
When no certificate matches the exact SNI host, the system SHALL select the certificate stored under the
host's parent domain (so a `*.example.com` certificate provided as `example.com` serves its subdomains),
falling back to the self-signed default when neither matches.

#### Scenario: Parent-domain certificate serves a subdomain
- **WHEN** a certificate is stored under `example.com`, no exact certificate exists for `foo.example.com`, and a handshake requests `foo.example.com`
- **THEN** the `example.com` certificate is selected

#### Scenario: Fallback when nothing matches
- **WHEN** a handshake requests a host with no exact and no parent-domain certificate
- **THEN** the self-signed fallback certificate is selected

### Requirement: TLS protocol and cipher hardening
The system SHALL apply configurable TLS hardening to the HTTPS endpoint: a minimum TLS version (default TLS
1.2), the enabled HTTP protocols (default HTTP/1.1 and HTTP/2), and an optional cipher-suite allow-list. The
minimum version SHALL map to the corresponding enabled TLS protocols; the cipher allow-list SHALL be applied
only on platforms that support explicit cipher selection and otherwise ignored.

#### Scenario: Minimum TLS version maps to enabled protocols
- **WHEN** the minimum TLS version is configured as TLS 1.2
- **THEN** the HTTPS endpoint enables TLS 1.2 and TLS 1.3

#### Scenario: Minimum TLS 1.3 excludes older protocols
- **WHEN** the minimum TLS version is configured as TLS 1.3
- **THEN** the HTTPS endpoint enables only TLS 1.3

### Requirement: HTTPS-only hosts are not provisioned
The system SHALL exclude a host whose HTTPS method is `nohttps` from certificate provisioning, since it is
not served over HTTPS.

#### Scenario: nohttps host is skipped
- **WHEN** a route declares a certificate host but sets the HTTPS method to `nohttps`
- **THEN** that host is not included in the set of desired certificates

### Requirement: Client certificate CA validation
When a client CA certificate is configured, the system SHALL make the HTTPS endpoint request a client
certificate and SHALL accept a presented certificate only when it chains to the configured CA; a
certificate that does not chain to the CA SHALL be rejected. When no client CA is configured, no client
certificate is requested.

#### Scenario: Certificate chaining to the CA is accepted
- **WHEN** a client certificate signed by the configured CA is validated
- **THEN** validation succeeds

#### Scenario: Certificate not chaining to the CA is rejected
- **WHEN** a client certificate not signed by the configured CA is validated
- **THEN** validation fails

### Requirement: Resilient concurrent provisioning
The system SHALL provision certificates for multiple hosts concurrently, with a bounded degree of parallelism,
so that one host's slow or failing ACME validation does not delay or block provisioning for the other hosts.
Per-host failures SHALL remain isolated — logged, and never fatal to the pass or the other hosts.

#### Scenario: A slow host does not block others
- **WHEN** several hosts need certificates and one host's ACME validation is slow
- **THEN** the other hosts are provisioned without waiting for the slow one

#### Scenario: A failing host does not affect others
- **WHEN** one host's provisioning throws
- **THEN** the failure is logged and the other hosts are still provisioned

#### Scenario: Concurrency is bounded
- **WHEN** many hosts need certificates at once
- **THEN** the number of simultaneous ACME requests does not exceed the configured bound

