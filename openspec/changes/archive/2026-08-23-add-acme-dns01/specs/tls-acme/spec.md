## ADDED Requirements

### Requirement: ACME DNS-01 challenge

The system SHALL support the ACME DNS-01 challenge as an alternative to HTTP-01, selected per host via
`DOCKYARP_ACME_CHALLENGE` (`http-01` default, `dns-01` opt-in; an unrecognized value SHALL fall back to
`http-01` and log a warning, not fail provisioning). For a `dns-01` host, the system SHALL publish a TXT
record for `_acme-challenge.<host>` (or `_acme-challenge.<parent-domain>` for a wildcard host) via the
configured DNS provider before requesting validation, and SHALL remove that record once validation
completes, regardless of outcome.

#### Scenario: DNS-01 challenge is answered

- **WHEN** a host is configured with `DOCKYARP_ACME_CHALLENGE=dns-01`
- **THEN** the system publishes the expected `_acme-challenge` TXT record via the configured DNS provider,
  the ACME provider validates it, and the issued certificate is stored for the host

#### Scenario: Challenge record is removed after validation

- **WHEN** DNS-01 validation completes, whether it succeeds or fails
- **THEN** the published `_acme-challenge` TXT record is removed from the DNS provider

#### Scenario: Unrecognized challenge type falls back to HTTP-01

- **WHEN** a host declares `DOCKYARP_ACME_CHALLENGE` with an unrecognized value
- **THEN** the system provisions that host via HTTP-01 and logs a warning, rather than failing provisioning

### Requirement: Wildcard certificate issuance via DNS-01

The system SHALL issue a wildcard certificate (`*.example.com`) for a host declaring that identifier as its
`CertificateHost` with `DOCKYARP_ACME_CHALLENGE=dns-01` (a wildcard identifier is only valid with DNS-01 —
this is an ACME protocol constraint, not a DockYarp-specific restriction) and SHALL store the issued
certificate under the parent domain (`example.com`), matching the existing wildcard parent certificate
selection lookup used for operator-provided wildcard certificates.

#### Scenario: Wildcard certificate is issued and served for a subdomain

- **WHEN** a host declares `CertificateHost = "*.example.com"` with `DOCKYARP_ACME_CHALLENGE=dns-01`
- **THEN** the system requests and stores a wildcard certificate under `example.com`, and a TLS handshake
  for `foo.example.com` is served that certificate via the existing parent-domain SNI fallback

### Requirement: DNS provider configuration (RFC 2136)

The system SHALL support RFC 2136 (Dynamic DNS Update) as a DNS-01 challenge provider, configured globally
(`Tls:DnsUpdateServer`, `Tls:DnsUpdateZone`, `Tls:DnsUpdateTsigKeyName`, `Tls:DnsUpdateTsigKeySecret`,
`Tls:DnsUpdateTsigAlgorithm`) — not per host, since DNS infrastructure is an operator-level concern. When a
host is configured for `dns-01` but the DNS provider configuration is missing or incomplete, provisioning
for that host SHALL fail with a clear, actionable error, and provisioning for other hosts (HTTP-01 or
already-DNS-01-configured) SHALL be unaffected.

#### Scenario: Missing DNS provider configuration fails only the affected host

- **WHEN** a host declares `DOCKYARP_ACME_CHALLENGE=dns-01` and no RFC 2136 server is configured
- **THEN** provisioning for that host fails with an error identifying the missing configuration, and
  HTTP-01 hosts continue to provision normally

## MODIFIED Requirements

### Requirement: ACME certificate acquisition

The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge (default) or the DNS-01 challenge (per-host opt-in via `DOCKYARP_ACME_CHALLENGE`),
and SHALL store the resulting certificate in the certificate store as a PEM pair (`{host}.crt` for the full
chain, `{host}.key` for the private key), not as a PFX/PKCS12 file. A wildcard `CertificateHost`
(`*.example.com`, DNS-01 only) SHALL be stored under its parent domain (`example.com`), stripped of the
leading `*.`. **When `Tls:PrivateKeyEncryptionPassphrase` is configured, the private key SHALL be written as
an encrypted PKCS8 PEM (`ENCRYPTED PRIVATE KEY`); when unset, it SHALL be written as plain PKCS8 PEM,
unchanged from today.** Acquisition SHALL succeed regardless of whether the ACME provider's response
includes its own self-signed root certificate — a root is not required to be present, and its absence SHALL
NOT cause any intermediate the provider did return to be discarded. When the issued chain includes one or
more intermediates, the system SHALL preserve and serve them during the TLS handshake the same way a
provided (PEM/PFX) certificate with a chain is served — ACME-issued and operator-provided certificates SHALL
NOT differ in whether their chain reaches the client, and this SHALL hold independently of whether the ACME
response also happened to include a self-signed root.

#### Scenario: Certificate obtained for a labeled host

- **WHEN** a host declares TLS metadata (from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`) and no current certificate exists
- **THEN** the provisioning service requests a certificate for that host and stores it

#### Scenario: A provisioned certificate is persisted as PEM

- **WHEN** the provisioning service stores a newly acquired or renewed certificate for a host
- **THEN** it writes `{host}.crt` (leaf plus any intermediates) and `{host}.key` (the private key) as PEM text
  files in the certificate directory, not a `{host}.pfx` file

#### Scenario: The private key is encrypted at rest when configured

- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is set and a certificate is stored
- **THEN** `{host}.key` is an `ENCRYPTED PRIVATE KEY` PEM, not readable without the passphrase, and DockYarp
  still serves TLS for that host correctly (it decrypts the key itself at load)

#### Scenario: HTTP-01 challenge is answered

- **WHEN** the ACME provider requests validation of a token
- **THEN** the challenge is served at `/.well-known/acme-challenge/{token}` with the expected key authorization

#### Scenario: Private CA whose root is not in the issued chain

- **WHEN** the ACME provider issues a certificate whose chain includes an intermediate but does **not** include
  a self-signed root (a private or custom CA following normal ACME convention — the root is trusted out of
  band, not distributed via the protocol)
- **THEN** the system still stores a usable certificate for the host, and the intermediate the provider did
  return is preserved, not discarded — the certificate served for that host is not leaf-only just because no
  root was present in the response

#### Scenario: An ACME-issued intermediate is sent during the handshake

- **WHEN** the ACME provider issues a certificate whose chain includes an intermediate, and that certificate is
  stored and later selected for a TLS handshake
- **THEN** a client trusting only the CA root (not the intermediate, and with no other source for it) can build
  a complete chain from what the server sends during that handshake alone — this holds whether or not the
  ACME response itself included that root

#### Scenario: A wildcard identifier is stored under its parent domain

- **WHEN** a host declares `CertificateHost = "*.example.com"` and DNS-01 issuance succeeds
- **THEN** the stored certificate key is `example.com`, not `*.example.com`
