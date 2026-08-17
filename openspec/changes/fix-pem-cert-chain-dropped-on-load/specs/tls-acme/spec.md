## MODIFIED Requirements

### Requirement: Provided certificate loading
The system SHALL load operator-provided certificates from the certificate directory at startup: PEM pairs
(`{host}.crt` with a matching `{host}.key`) and `{host}.pfx` files, keyed by the file name (the host). A
`.crt` without a matching `.key` SHALL be skipped. A provided certificate SHALL take precedence over an
ACME-persisted certificate for the same host. **When a `.crt` file contains more than one certificate (a full
chain — leaf plus one or more intermediates, the standard shape produced by nginx-proxy/acme-companion, acme.sh,
and other real ACME clients), the system SHALL preserve and serve the complete chain, not only the first
certificate in the file.**

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
