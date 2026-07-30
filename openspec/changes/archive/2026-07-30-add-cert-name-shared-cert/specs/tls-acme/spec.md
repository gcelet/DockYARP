## ADDED Requirements

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
