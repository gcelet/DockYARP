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

### Requirement: ACME HTTP-01 challenge serving
The system SHALL serve ACME HTTP-01 challenges from its token store independently of host routing: a challenge
is answered whenever its token is in the store, even for a host that has no matching route (the store only
holds tokens the system is itself provisioning). Serving SHALL be enabled by default and MAY be disabled via
`Tls:Http01ChallengeEnabled`; when disabled, a request to the challenge path SHALL return 404 instead of the
token.

#### Scenario: Challenge answered regardless of host routing
- **WHEN** an HTTP-01 request arrives for a token present in the store
- **THEN** the key authorization is served, independent of whether the requested host has a route

#### Scenario: Challenge serving disabled
- **WHEN** `Tls:Http01ChallengeEnabled` is `false` and a request reaches the ACME challenge path
- **THEN** the response is 404, even for a token that is present in the store

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
The system SHALL present a default certificate for handshakes targeting a host without a specific certificate,
so the connection is handled deterministically rather than aborting. When an operator-supplied `default.crt`
and `default.key` exist in the certificate directory, the system SHALL present that certificate as the
fallback; otherwise it SHALL present a generated self-signed certificate. The `default` basename SHALL be
reserved for this fallback and SHALL NOT be registered as a per-host certificate.

#### Scenario: Handshake for an unknown host
- **WHEN** a TLS handshake targets a host that has no stored certificate
- **THEN** the selector returns the default fallback certificate

#### Scenario: Operator-supplied default certificate is preferred
- **WHEN** `default.crt` and `default.key` are present in the certificate directory
- **THEN** the fallback presented for unknown hosts is the operator certificate, not the self-signed one

#### Scenario: Self-signed default when none is supplied
- **WHEN** no `default.crt`/`default.key` is present
- **THEN** the fallback presented for unknown hosts is the generated self-signed certificate

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
only on platforms that support explicit cipher selection and otherwise ignored. The system SHALL also accept a
named `Tls:SslPolicy` preset (Mozilla `Modern`, `Intermediate`, `Old`) that sets the minimum TLS version and a
default cipher-suite list; an explicit cipher-suite allow-list SHALL override the preset's ciphers, and an
unrecognized or unset policy SHALL leave the configured values unchanged.

#### Scenario: Minimum TLS version maps to enabled protocols
- **WHEN** the minimum TLS version is configured as TLS 1.2
- **THEN** the HTTPS endpoint enables TLS 1.2 and TLS 1.3

#### Scenario: Minimum TLS 1.3 excludes older protocols
- **WHEN** the minimum TLS version is configured as TLS 1.3
- **THEN** the HTTPS endpoint enables only TLS 1.3

#### Scenario: Modern preset selects TLS 1.3
- **WHEN** `Tls:SslPolicy` is `Mozilla-Modern`
- **THEN** the effective minimum version is TLS 1.3 and the cipher list is the TLS 1.3 suites

#### Scenario: Explicit ciphers override the preset
- **WHEN** `Tls:SslPolicy` names a preset and `Tls:CipherSuites` is also set
- **THEN** the explicit cipher list is used instead of the preset's ciphers

#### Scenario: Unknown policy falls back
- **WHEN** `Tls:SslPolicy` is unset or unrecognized
- **THEN** the configured minimum version and cipher allow-list are used unchanged

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

### Requirement: Named shared certificate (CERT_NAME)
The system SHALL let a host pin its TLS certificate to a named certificate in the store via `CERT_NAME`, so
several hosts can share one SAN/wildcard certificate. During SNI selection, a host whose route carries a
`CERT_NAME` SHALL be served the certificate stored under that name, taking precedence over the per-host and
wildcard-parent lookup. A host carrying `CERT_NAME` SHALL be treated as an HTTPS host and SHALL NOT be
individually provisioned via ACME. When the named certificate is absent, selection SHALL fall back to the
per-host rules.

#### Scenario: Shared certificate served by name
- **WHEN** two hosts set `CERT_NAME=shared` and a `shared` certificate exists in the store
- **THEN** the SNI handshake for either host is served the `shared` certificate

#### Scenario: Missing named certificate falls back
- **WHEN** a host sets `CERT_NAME` but no certificate exists under that name
- **THEN** SNI selection falls back to the per-host / wildcard-parent rules (and the default fallback if none)

#### Scenario: CERT_NAME host is not provisioned via ACME
- **WHEN** a host carries `CERT_NAME`
- **THEN** it is excluded from the ACME desired-certificate set (the operator supplies the shared certificate)

### Requirement: Per-connection TLS session assembly
The system SHALL assemble the TLS session for each HTTPS connection — the server certificate, the enabled TLS
protocols, the cipher policy, and the client-certificate policy — from the connection's SNI host at handshake
time, defaulting to the globally-configured TLS posture when the host declares no override. The plaintext HTTP
endpoint SHALL be served over HTTP/1.1 without TLS. Data-plane endpoints SHALL be bound from the configured
HTTP and HTTPS ports.

#### Scenario: Per-connection assembly preserves the global posture
- **WHEN** a TLS connection targets a host that declares no TLS override
- **THEN** the connection presents the certificate selected by SNI, the globally-configured minimum TLS
  protocol version and cipher policy, and the globally-configured client-certificate behavior

#### Scenario: Mutual TLS is preserved through the callback
- **WHEN** a client CA is configured and a client presents a certificate chaining to that CA
- **THEN** the connection is accepted, and a certificate that does not chain to the CA is rejected

#### Scenario: The HTTP endpoint stays plaintext HTTP/1.1
- **WHEN** a request reaches the HTTP endpoint (ACME challenge or redirect)
- **THEN** it is served over HTTP/1.1 without TLS

### Requirement: Per-host TLS policy (SSL_POLICY)
The system SHALL recognize a per-container `SSL_POLICY` value — set as an environment variable or a label, with
the environment variable taking precedence — that names a TLS preset. During the SNI handshake for a host that
declares a recognized preset, the system SHALL negotiate with that preset's minimum TLS version and cipher
policy, overriding the global TLS posture; a host that declares no `SSL_POLICY` SHALL use the global posture. An
unrecognized per-host `SSL_POLICY` SHALL be ignored (the global posture applies) with a one-time diagnostic.
Per-host `SSL_POLICY` is honored for hosts that are TLS-configured (declare `LETSENCRYPT_HOST` or `CERT_NAME`),
consistent with the other per-host TLS attributes, and SHALL NOT create an ACME certificate desire.

#### Scenario: Per-host preset narrows negotiation
- **WHEN** a certified host declares `SSL_POLICY=Mozilla-Modern`
- **THEN** its handshake enables only TLS 1.3, while a host without the override keeps the global posture

#### Scenario: Unknown per-host policy falls back to global
- **WHEN** a host declares an unrecognized `SSL_POLICY`
- **THEN** the global posture applies and a one-time diagnostic is emitted

#### Scenario: Environment value wins over a same-named label
- **WHEN** a container sets `SSL_POLICY` as both an environment variable and a label
- **THEN** the environment variable's preset is used

### Requirement: Per-host HTTP/2 toggle
The system SHALL recognize a per-container HTTP/2 toggle — the `DOCKYARP_HTTP2` label or the nginx-proxy
`com.github.nginx-proxy.nginx-proxy.http2.enable` alias, a boolean, resolved from the container's merged
configuration — that controls whether HTTP/2 is offered to clients for a host. During the SNI handshake for a host
that sets the toggle to **false**, the system SHALL advertise only HTTP/1.1 via ALPN, overriding the global protocol
set; a host that leaves the toggle unset SHALL advertise the globally-configured protocols. Because HTTP/2 support is
bound at the HTTPS listener from the global protocol set, the toggle SHALL only **narrow** the offered protocols —
setting it to true has no effect unless HTTP/2 is enabled globally. The toggle is honored for TLS-configured hosts
(declaring `LETSENCRYPT_HOST` or `CERT_NAME`), consistent with the other per-host TLS attributes, and SHALL NOT
create an ACME certificate desire.

#### Scenario: Per-host disable narrows ALPN to HTTP/1.1
- **WHEN** a certified host disables HTTP/2 (`DOCKYARP_HTTP2=false`)
- **THEN** its handshake advertises only HTTP/1.1 via ALPN, while a host without the override keeps the global
  protocols

#### Scenario: Default host keeps the global protocols
- **WHEN** a host leaves the HTTP/2 toggle unset
- **THEN** its handshake advertises the globally-configured protocols (HTTP/2 offered by default)

#### Scenario: Enabling beyond the global set is a no-op
- **WHEN** a host sets the toggle to true while HTTP/2 is disabled globally
- **THEN** the host still negotiates HTTP/1.1 only (the listener does not offer HTTP/2)

