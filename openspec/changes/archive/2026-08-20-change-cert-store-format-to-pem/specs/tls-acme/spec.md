## MODIFIED Requirements

### Requirement: ACME certificate acquisition
The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge, and SHALL store the resulting certificate in the certificate store **as a PEM pair
(`{host}.crt` for the full chain, `{host}.key` for the private key), not as a PFX/PKCS12 file.** Acquisition
SHALL succeed regardless of whether the ACME provider's response includes its own self-signed root certificate
— a root is not required to be present, and its absence SHALL NOT cause any intermediate the provider did
return to be discarded. When the issued chain includes one or more intermediates, the system SHALL preserve
and serve them during the TLS handshake the same way a provided (PEM/PFX) certificate with a chain is served —
ACME-issued and operator-provided certificates SHALL NOT differ in whether their chain reaches the client, and
this SHALL hold independently of whether the ACME response also happened to include a self-signed root.

#### Scenario: Certificate obtained for a labeled host
- **WHEN** a host declares TLS metadata (from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`) and no current certificate exists
- **THEN** the provisioning service requests a certificate for that host and stores it

#### Scenario: A provisioned certificate is persisted as PEM
- **WHEN** the provisioning service stores a newly acquired or renewed certificate for a host
- **THEN** it writes `{host}.crt` (leaf plus any intermediates) and `{host}.key` (the private key) as PEM text
  files in the certificate directory, not a `{host}.pfx` file

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
required for the chain to reach the client.

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
