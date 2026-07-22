## ADDED Requirements

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
