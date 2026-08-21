---
id: add-tls-private-key-encryption
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
nginx-proxy: n/a (DockYarp value-add)
status: backlog
provenance: 2026-08-20 user feedback, same session as `change-cert-store-format-to-pem` and the
  admin-dashboard cert-management items — a passphrase to secure the TLS-serving certificates' private keys at
  rest, mirroring the existing `DataProtection:CertificatePassword` pattern
---

## Why

`FileCertificateStore` persists every TLS-serving certificate's private key as plain, unencrypted PEM
(`{host}.key`, since `change-cert-store-format-to-pem`) or previously as a password-less PFX — either way,
readable by anyone with filesystem/volume access to the certs directory. The user wants an operator-configured
passphrase to encrypt this key material at rest, the same way `DataProtection:CertificatePassword` already
protects the separate Data Protection key ring.

## Current state

- `src/DockYarp.Security/DataProtectionSetup.cs`/`DataProtectionOptions.cs` already implement this exact
  pattern for a *different* key ring (Data Protection, not TLS): `DataProtection:CertificatePath` +
  `CertificatePassword`, optional, fails fast if configured but unloadable. A real, working precedent to mirror
  the shape of (config surface, fail-fast behavior), not to copy verbatim (different key material).
- `FileCertificateStore.Save()`/`ConvertToPem()`/`Load()` (`src/DockYarp.Tls/FileCertificateStore.cs`,
  `LoadedCertificatePem.cs`) currently read/write the private key as plain PKCS8 PEM — no encryption anywhere
  in this path.

## Proposed change (sketch)

Not designed — needs propose-time decisions. Key nuance already reasoned through this session, explicit here
so it isn't silently forgotten or oversold: **encrypting the TLS-serving certificate's own private key does
NOT defend against the admin-dashboard-download threat model** (`add-admin-dashboard-cert-download`) — DockYarp
must be able to decrypt the key itself, automatically, at startup, to actually serve TLS handshakes, so
whatever passphrase/mechanism unlocks it has to live in the same config/secrets trust domain the app itself
runs in. An attacker who can already reach the dashboard is, in practice, operating in a similar trust domain
to whatever holds that passphrase. The **real, honest value** of this feature is narrower: protecting the key
from someone with *only* filesystem/volume/backup access and *not* the running app's own configuration — the
same value `DataProtection:CertificatePassword` already provides for its own key ring. State this distinction
explicitly wherever this item is described (proposal, docs) — do not let the feature imply it closes the
dashboard-download risk, which it doesn't.

Candidate shape: a new `Tls:PrivateKeyEncryptionCertificatePath`/`...Password` (or similar; exact naming TBD)
config surface, optional/opt-in, PKCS8-encrypted-PEM (`ExportEncryptedPkcs8PrivateKeyPem`) instead of the
current plain export when configured; fails fast at startup if configured but unloadable, matching
`DataProtectionSetup`'s existing precedent.

## Acceptance criteria (→ scenarios)

- **WHEN** the encryption passphrase/certificate is configured **THEN** newly persisted private keys are
  encrypted at rest, and DockYarp still starts and serves TLS correctly using them (auto-decrypts at load).
- **WHEN** it is not configured (default) **THEN** behavior is unchanged from today (plain PEM), no regression.
- **WHEN** it is configured but the encryption certificate/passphrase can't be loaded **THEN** startup fails
  fast with an actionable error, not a silent fallback to unencrypted keys.

## Notes / risks / references

- Depends conceptually on `change-cert-store-format-to-pem` (PEM is now the write format this would encrypt)
  but is otherwise independent of the two dashboard cert-management items — do not assume it needs to ship
  alongside or gate them.
- Mirror `src/DockYarp.Security/DataProtectionSetup.cs`'s fail-fast pattern; do not invent a second validation
  mechanism for what is conceptually the same kind of guard.
