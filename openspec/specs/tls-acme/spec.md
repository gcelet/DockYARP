# tls-acme Specification

## Purpose
TBD - created by archiving change add-tls-acme. Update Purpose after archive.
## Requirements
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

### Requirement: ACME account persistence
The system SHALL persist an ACME account key per **(resolved contact email, ACME directory endpoint)** pair
and reuse it for every certificate request and renewal sharing that same pair, rather than registering a new
account per request. A host's resolved contact email is its declared `LETSENCRYPT_EMAIL` (or the
capability's existing fallback to `Tls:ContactEmail` when unset) — unchanged from today's resolution. On
first use of a given (email, endpoint) pair with no persisted account key present for it, the system SHALL
generate one and persist it before making that ACME request. The persisted account key SHALL be stored on
the same operator-mounted volume as other DockYarp-persisted key material (the certificate directory), so it
survives a container restart or redeploy the same way stored certificates do. Changing `Tls:AcmeDirectoryUri`
to a different endpoint, or a host resolving to a different contact email than another host, SHALL use (or
generate) a separate persisted key for that (email, endpoint) pair, without disturbing a key already
persisted for a different pair.

#### Scenario: The same account is reused across requests sharing a contact email
- **WHEN** DockYarp requests a second certificate (a different host, or a renewal) that resolves to the same
  contact email and ACME directory endpoint as a prior request
- **THEN** the same ACME account is used, not a new one — verifiable against the CA by the account's own URL
  staying constant across those requests

#### Scenario: Hosts with different contact emails get independent accounts
- **WHEN** two hosts resolve to different contact emails (whether via distinct `LETSENCRYPT_EMAIL` values, or
  one declaring none and falling back to `Tls:ContactEmail` while the other declares an explicit one)
- **THEN** each host's requests use its own separate persisted account, matching today's behavior where each
  request's declared email is honored on its own account

#### Scenario: Switching ACME directory endpoints does not disturb a previously used one's account keys
- **WHEN** an operator changes `Tls:AcmeDirectoryUri` to a different ACME endpoint than the one previously in
  use
- **THEN** DockYarp generates (or reuses, if one is already present) persisted account keys scoped to the new
  endpoint, and every account key persisted for the previous endpoint remains on disk, untouched

#### Scenario: First run generates and persists an account key
- **WHEN** DockYarp makes its first-ever ACME request for a given (contact email, ACME directory endpoint)
  pair, with no persisted account key yet present for it
- **THEN** an account key is generated and persisted for that pair before the request is made

#### Scenario: A persisted account key survives a restart
- **WHEN** DockYarp restarts with a previously persisted account key present for a given (contact email,
  endpoint) pair
- **THEN** a request resolving to that same pair reuses that same account key (and therefore the same ACME
  account) rather than generating a new one

### Requirement: ACME account import (EC-keyed accounts only)
The system SHALL allow an operator to migrate an existing **EC (P-256)** ACME account by placing that
account's PEM private key at the persisted-account-key location matching the migrating host's resolved
contact email and ACME directory endpoint, before DockYarp's first ACME request for that (email, endpoint)
pair, so DockYarp continues using that account (via RFC 8555 `newAccount` idempotency) instead of registering
a new one. An account key using an algorithm other than EC P-256 (for example RSA, the default for some
third-party ACME clients when no EC key length was explicitly requested at registration) is **not**
supported for import — DockYarp SHALL treat an unsupported key algorithm at that location as a configuration
error, not silently ignore it and generate a new account.

#### Scenario: An imported EC account key is reused instead of generating a new account
- **WHEN** an operator places an existing EC (P-256) ACME account's PEM private key at the persisted-account-
  key location matching a host's resolved contact email and ACME directory endpoint, before DockYarp's first
  ACME request for that pair
- **THEN** DockYarp's first ACME request for that pair reuses that existing account (same account URL as the
  CA already has on record for that key) rather than registering a new one

#### Scenario: An unsupported key algorithm at the import location fails clearly
- **WHEN** a PEM private key using an algorithm other than EC P-256 (for example RSA) is present at the
  persisted-account-key location
- **THEN** DockYarp fails with an actionable error identifying the unsupported algorithm, rather than
  silently generating a new account key

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
ACME-persisted certificate for the same host. **When both a `{host}.crt`/`{host}.key` PEM pair and a
`{host}.pfx` file are present for the same host — for example a legacy PFX left over from before this system
switched to persisting PEM, alongside a freshly provisioned PEM pair for the same host — the PEM pair SHALL
take precedence; loading SHALL NOT depend on file enumeration order.** When a `.crt` file contains more than
one certificate (a full chain — leaf plus one or more intermediates, the standard shape produced by
nginx-proxy/acme-companion, acme.sh, and other real ACME clients), the system SHALL preserve and serve the
complete chain, not only the first certificate in the file. The preserved chain SHALL be sent during the TLS
handshake for that host, not merely retained on the loaded certificate object — loading and serving are both
required for the chain to reach the client. **A `{host}.key` file SHALL be loadable whether it is plain or
encrypted PKCS8 PEM, determined from the PEM's own label (`PRIVATE KEY` vs `ENCRYPTED PRIVATE KEY`), never
from whether `Tls:PrivateKeyEncryptionPassphrase` happens to be configured — an operator-provided plain key
SHALL keep loading correctly even when encryption is configured. An encrypted key SHALL be decrypted with
`Tls:PrivateKeyEncryptionPassphrase`; if that fails and `Tls:PreviousPrivateKeyEncryptionPassphrase` is
configured, decryption SHALL be retried with it before the key is treated as unloadable — so rotating the
passphrase does not strand every already-encrypted key at the next restart.**

#### Scenario: PEM pair is loaded
- **WHEN** `app.local.crt` and `app.local.key` are present in the certificate directory
- **THEN** the store serves a certificate for `app.local` with a usable private key

#### Scenario: PFX file is loaded
- **WHEN** `app.local.pfx` is present in the certificate directory with no matching `.crt`/`.key` pair
- **THEN** the store serves a certificate for `app.local`

#### Scenario: A PEM pair wins over a legacy PFX for the same host
- **WHEN** both `app.local.crt`/`app.local.key` and `app.local.pfx` are present in the certificate directory
- **THEN** the certificate served for `app.local` is the one loaded from the PEM pair, not the PFX file

#### Scenario: Unpaired certificate file is skipped
- **WHEN** `app.local.crt` is present without a matching `app.local.key`
- **THEN** no certificate is loaded for `app.local` from that file

#### Scenario: A full-chain PEM file preserves its intermediate
- **WHEN** `app.local.crt` contains a leaf certificate followed by an intermediate certificate (a full chain)
  and `app.local.key` is the leaf's matching private key
- **THEN** the certificate served for `app.local` includes the intermediate, so a client that trusts the
  issuing root but not the intermediate out of band can still build a complete chain

#### Scenario: The chain is actually sent during the handshake
- **WHEN** a TLS handshake targets a host whose loaded certificate has an intermediate
- **THEN** a client trusting only the CA root (not the intermediate, and with no other source for it) can build
  a complete chain from what the server sends during that handshake alone — verified at the wire level, not
  only by inspecting the loaded certificate object in isolation

#### Scenario: An operator-provided plain key still loads when encryption is configured
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is set and an operator drops in a plain (unencrypted)
  `{host}.crt`/`{host}.key` pair
- **THEN** the key loads correctly — the loader decides plain-vs-encrypted from the PEM's own label, not from
  whether encryption is configured

#### Scenario: A key encrypted with the current passphrase loads
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is set and `{host}.key` is an `ENCRYPTED PRIVATE KEY` PEM
  encrypted with that same passphrase
- **THEN** the key loads and the certificate is served correctly

#### Scenario: Passphrase rotation falls back to the previous passphrase
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` has been changed to a new value, `{host}.key` is still encrypted
  with the old one, and `Tls:PreviousPrivateKeyEncryptionPassphrase` is set to that old value
- **THEN** the key still loads (decrypted via the previous passphrase) and the certificate is served correctly

#### Scenario: An unloadable encrypted key fails fast with an actionable error
- **WHEN** `{host}.key` is encrypted and neither the current nor the previous configured passphrase decrypts it
- **THEN** loading that key fails with an error identifying the host and file, not a silent skip

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

The recognized presets SHALL also include the classic AWS ELB (ALB) security-policy names. Because the system floors
at TLS 1.2 (it never enables TLS 1.0/1.1), each ELB policy SHALL map to a TLS-version floor of `Tls13` for the
TLS-1.3-only policy and `Tls12` for every other policy, with a best-effort cipher-suite list expressed as IANA suite
names. Specialized FIPS, post-quantum, and RFC 9151 ELB variants SHALL NOT be recognized (they keep the
unrecognized-policy fallback). The same preset table applies to both the global `Tls:SslPolicy` and the per-host
`SSL_POLICY`.

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

#### Scenario: AWS ELB policy name is recognized
- **WHEN** `Tls:SslPolicy` (or a host's `SSL_POLICY`) is a classic AWS ELB name such as
  `ELBSecurityPolicy-TLS13-1-2-2021-06`
- **THEN** the effective minimum version is the policy's floor clamped to TLS 1.2 (or TLS 1.3 for the 1.3-only policy)
  with a best-effort cipher list

#### Scenario: TLS-1.3-only ELB policy selects TLS 1.3
- **WHEN** the policy is `ELBSecurityPolicy-TLS13-1-3-2021-06`
- **THEN** the effective minimum version is TLS 1.3

#### Scenario: Specialized ELB variant falls back
- **WHEN** the policy is a FIPS, post-quantum, or RFC 9151 ELB variant (for example `ELBSecurityPolicy-TLS13-1-2-FIPS-2023-04`)
- **THEN** it is not recognized and the configured values are used unchanged

### Requirement: HTTPS-only hosts are not provisioned
The system SHALL exclude a host whose HTTPS method is `nohttps` from certificate provisioning, since it is
not served over HTTPS.

#### Scenario: nohttps host is skipped
- **WHEN** a route declares a certificate host but sets the HTTPS method to `nohttps`
- **THEN** that host is not included in the set of desired certificates

### Requirement: Client certificate CA validation
When a client CA certificate is configured, the system SHALL request a client certificate at the TLS handshake
for any host whose client-certificate requirement is `required` or `optional`; a host whose requirement is
`none` SHALL NOT be prompted for one. For a `required` host, the system SHALL accept a presented certificate
only when it chains to the configured CA and is not revoked per the configured CRL; a certificate that does not
chain to the CA, or that is revoked, SHALL cause the handshake to fail. For an `optional` host, the system SHALL
accept the handshake regardless of whether a presented certificate chains to the CA or is revoked, deferring the
verification outcome to the application layer. When no client CA is configured, no client certificate is
requested for any host.

#### Scenario: Certificate chaining to the CA is accepted
- **WHEN** a client certificate signed by the configured CA is validated
- **THEN** validation succeeds

#### Scenario: Certificate not chaining to the CA is rejected
- **WHEN** a client certificate not signed by the configured CA is validated
- **THEN** validation fails

#### Scenario: Revoked certificate is rejected
- **WHEN** a client certificate's serial number is listed in the configured CRL
- **THEN** validation fails, even if the certificate otherwise chains to the configured CA

#### Scenario: Required host fails the handshake for an untrusted certificate
- **WHEN** a client presents a certificate that does not chain to the configured CA to a host whose requirement
  is `required`
- **THEN** the TLS handshake fails and the connection is not established

#### Scenario: Optional host accepts the handshake despite an untrusted certificate
- **WHEN** a client presents a certificate that does not chain to the configured CA to a host whose requirement
  is `optional`
- **THEN** the TLS handshake succeeds and the connection is established

#### Scenario: Host with no client-certificate requirement is not prompted
- **WHEN** a client connects to a host whose client-certificate requirement is `none`
- **THEN** the TLS handshake does not request a client certificate

### Requirement: Resilient concurrent provisioning
The system SHALL provision certificates for multiple hosts concurrently, with a bounded degree of parallelism,
so that one host's slow or failing ACME validation does not delay or block provisioning for the other hosts.
Per-host failures SHALL remain isolated — logged, and never fatal to the pass or the other hosts. A per-host
provisioning failure that the reconcile loop resolves on a subsequent attempt (a **transient** failure, such as a
startup validation race) SHALL be logged at **Warning** with a short reason and **without** a stack trace; a failure
that **persists** across repeated attempts (beyond a small threshold of consecutive failures) SHALL escalate to
**Error** with the exception. A successful provisioning SHALL reset the host's consecutive-failure count.

#### Scenario: A slow host does not block others
- **WHEN** several hosts need certificates and one host's ACME validation is slow
- **THEN** the other hosts are provisioned without waiting for the slow one

#### Scenario: A failing host does not affect others
- **WHEN** one host's provisioning throws
- **THEN** the failure is logged and the other hosts are still provisioned

#### Scenario: Concurrency is bounded
- **WHEN** many hosts need certificates at once
- **THEN** the number of simultaneous ACME requests does not exceed the configured bound

#### Scenario: A transient failure is logged at Warning
- **WHEN** a host's provisioning fails but a later attempt succeeds
- **THEN** the transient failure is logged at Warning without a stack trace (not a misleading Error), and the host is
  ultimately provisioned

#### Scenario: A persistent failure escalates to Error
- **WHEN** a host's provisioning keeps failing beyond the transient threshold of consecutive attempts
- **THEN** the failure is logged at Error with the exception

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
time, defaulting to the globally-configured TLS posture when the host declares no override. The
client-certificate policy SHALL be resolved per host from that host's `required`/`optional`/`none` requirement,
not applied uniformly across every host. The plaintext HTTP endpoint SHALL be served over HTTP/1.1 without TLS.
Data-plane endpoints SHALL be bound from the configured HTTP and HTTPS ports.

#### Scenario: Per-connection assembly preserves the global posture
- **WHEN** a TLS connection targets a host that declares no TLS override
- **THEN** the connection presents the certificate selected by SNI, the globally-configured minimum TLS
  protocol version and cipher policy, and the globally-configured client-certificate behavior

#### Scenario: Mutual TLS is preserved through the callback
- **WHEN** a client CA is configured and a client connects to a host whose requirement is `required`
- **THEN** the connection is accepted only when the presented certificate chains to that CA and is not revoked;
  otherwise the connection is rejected

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

### Requirement: Reserved (non-route) certificate hosts
The system SHALL support provisioning certificates for **reserved** hosts that are not derived from routes — hosts the
proxy serves itself, such as the dedicated admin host — through a certificate-desire contribution point, without the
TLS layer depending on the admin subsystem. A reserved host SHALL be reconciled by the same provisioning/renewal loop
as route hosts (requested via ACME when missing, renewed before expiry, served via SNI once stored). Contribution
SHALL be **opt-in**: when no reserved host is contributed, provisioning behaves exactly as for routes alone. When a
reserved host duplicates a route host, it SHALL NOT cause a second provisioning of the same host.

#### Scenario: Reserved host is provisioned like a route host
- **WHEN** a reserved host is contributed (opted in) and has no current certificate
- **THEN** the provisioning service requests and stores a certificate for it, and renews it before expiry, exactly as
  it does for a host derived from a route

#### Scenario: No reserved host keeps route-only behavior
- **WHEN** no reserved host is contributed
- **THEN** the provisioning service reconciles exactly the route-derived hosts, unchanged

#### Scenario: Reserved host duplicating a route is not provisioned twice
- **WHEN** a reserved host equals a host already desired from a route
- **THEN** the merged desired set contains that host once (no duplicate provisioning)

### Requirement: Admin host certificate opt-in
The system SHALL let an operator opt the dedicated admin host (`AdminApi:Host`) into ACME certificate provisioning via
`AdminApi:LetsEncrypt`. When `AdminApi:LetsEncrypt` is enabled and `AdminApi:Host` is set, the admin host SHALL be
contributed as a reserved certificate host, using `AdminApi:ContactEmail` when provided and otherwise falling back to
`Tls:ContactEmail`. When the opt-in is disabled or the admin host is unset, no admin certificate SHALL be requested and
the admin host SHALL keep the default/operator-provided certificate.

#### Scenario: Opted-in admin host is contributed for provisioning
- **WHEN** `AdminApi:Host` is set and `AdminApi:LetsEncrypt` is `true`
- **THEN** the admin host is present in the reserved certificate hosts, with the resolved contact email

#### Scenario: Opt-in disabled requests no admin certificate
- **WHEN** `AdminApi:LetsEncrypt` is `false` (or `AdminApi:Host` is unset)
- **THEN** no admin host is contributed and the admin host keeps its default/operator certificate

