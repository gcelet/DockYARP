## ADDED Requirements

### Requirement: Private key re-encryption from the dashboard
The system SHALL support forcing a stored certificate's private key to be rewritten under the currently
configured `Tls:PrivateKeyEncryptionPassphrase`, triggered from the admin dashboard, gated by the same
`AdminApi:AllowCertificateConversion` opt-in as the PFX-to-PEM conversion action. This is a **rewrite of the
already-loaded certificate's on-disk representation only** — it SHALL NOT re-provision, renew, or otherwise
change which certificate is served for that host. The action SHALL be offered for every stored host whenever
`Tls:PrivateKeyEncryptionPassphrase` is configured (covering both a first-time enable of encryption on a
previously-plain key, and re-encrypting onto a newly rotated passphrase) — the action itself is never gated
per-host. The dashboard SHALL additionally indicate, per host, whether that host's key still needs the action
(loaded plain, or via the previous-passphrase fallback, rather than already under the current passphrase) —
this signal comes from what the loader already determined while decrypting the key at startup, not from a
fresh decryption attempt for display purposes. The action SHALL be a state-changing (POST) operation protected
by the framework's anti-forgery mechanism, the same as the PFX-to-PEM conversion action.

#### Scenario: Not available when no passphrase is configured
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is unset
- **THEN** no re-encryption action is available for any host

#### Scenario: Available for every host once a passphrase is configured
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is set and `AdminApi:AllowCertificateConversion` is `true`
- **THEN** the re-encryption action is offered for every stored host, regardless of whether that host's key is
  currently plain or already encrypted

#### Scenario: Re-encrypting a previously-plain key
- **WHEN** an operator triggers re-encryption for a host whose `{host}.key` is currently plain PEM
- **THEN** `{host}.key` becomes an `ENCRYPTED PRIVATE KEY` PEM under the current passphrase, and the same
  certificate continues to be served for that host afterward

#### Scenario: Re-encrypting after a passphrase rotation
- **WHEN** an operator triggers re-encryption for a host whose `{host}.key` is encrypted with a previous
  passphrase
- **THEN** `{host}.key` is rewritten encrypted under the current passphrase

#### Scenario: The re-encryption action is not exploitable via a forged link
- **WHEN** a request attempts to trigger the re-encryption action without a valid anti-forgery token
- **THEN** the request is rejected, not honored

#### Scenario: A host still needing re-encryption is indicated
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is configured and a host's `{host}.key` was loaded plain, or via
  `Tls:PreviousPrivateKeyEncryptionPassphrase`'s fallback, rather than the current passphrase
- **THEN** the dashboard indicates that host still needs re-encryption

#### Scenario: A host already on the current passphrase is not indicated
- **WHEN** `Tls:PrivateKeyEncryptionPassphrase` is configured and a host's `{host}.key` was loaded already
  encrypted with that same current passphrase
- **THEN** the dashboard does not indicate that host as needing re-encryption

#### Scenario: A PFX-backed host is never indicated
- **WHEN** a host's certificate is currently backed by a `.pfx` file rather than a PEM pair
- **THEN** the dashboard does not indicate that host as needing re-encryption, regardless of whether
  `Tls:PrivateKeyEncryptionPassphrase` is configured — converting it to PEM already applies the current
  passphrase

#### Scenario: The indication clears once the action is applied
- **WHEN** an operator triggers re-encryption for a host previously indicated as needing it
- **THEN** the dashboard no longer indicates that host as needing re-encryption
