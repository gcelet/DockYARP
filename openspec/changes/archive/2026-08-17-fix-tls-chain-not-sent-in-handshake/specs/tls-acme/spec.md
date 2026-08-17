## MODIFIED Requirements

### Requirement: Provided certificate loading
The system SHALL load operator-provided certificates from the certificate directory at startup: PEM pairs
(`{host}.crt` with a matching `{host}.key`) and `{host}.pfx` files, keyed by the file name (the host). A
`.crt` without a matching `.key` SHALL be skipped. A provided certificate SHALL take precedence over an
ACME-persisted certificate for the same host. When a `.crt` file contains more than one certificate (a full
chain — leaf plus one or more intermediates, the standard shape produced by nginx-proxy/acme-companion, acme.sh,
and other real ACME clients), the system SHALL preserve the complete chain, not only the first certificate in
the file. **The preserved chain SHALL be sent during the TLS handshake for that host, not merely retained on
the loaded certificate object — loading and serving are both required for the chain to reach the client.**

#### Scenario: PEM pair is loaded
- **WHEN** `app.local.crt` and `app.local.key` are present in the certificate directory
- **THEN** the store serves a certificate for `app.local` with a usable private key

#### Scenario: PFX file is loaded
- **WHEN** `app.local.pfx` is present in the certificate directory
- **THEN** the store serves a certificate for `app.local`

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
- **THEN** a client trusting only the CA root (not the intermediate, and with no other source for it) SHALL be
  able to build a complete chain from what the server sends during that handshake alone — verified at the wire
  level, not only by inspecting the loaded certificate object in isolation

### Requirement: ACME certificate acquisition
The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge, and SHALL store the resulting certificate in the certificate store. Acquisition SHALL
succeed whether or not the ACME provider's issued chain includes the CA root: when the full chain cannot be
completed to a root (as with a private or custom CA), the system SHALL fall back to storing the issued leaf
certificate rather than failing provisioning. **When the issued chain does include one or more intermediates,
the system SHALL preserve and serve them during the TLS handshake the same way a provided (PEM/PFX) certificate
with a chain is served — ACME-issued and operator-provided certificates SHALL NOT differ in whether their chain
reaches the client.**

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

#### Scenario: An ACME-issued intermediate is sent during the handshake
- **WHEN** the ACME provider issues a certificate whose chain includes an intermediate, and that certificate is
  stored and later selected for a TLS handshake
- **THEN** a client trusting only the CA root (not the intermediate, and with no other source for it) SHALL be
  able to build a complete chain from what the server sends during that handshake alone
