## Why

`FileCertificateStore` persists every TLS-serving certificate's private key as plain, unencrypted PEM
(readable by anyone with filesystem/volume access to the certs directory). The user wants an operator-configured
passphrase to encrypt this key material at rest, mirroring the value `DataProtection:CertificatePassword`
already provides for the separate Data Protection key ring — protection against volume/backup-only access, not
a defense against the admin-dashboard-download threat model (DockYarp must still auto-decrypt the key itself at
startup to serve TLS; that trust boundary is unchanged and this feature does not claim to close it).

## What Changes

- New `Tls:PrivateKeyEncryptionPassphrase` (opt-in, default unset — unchanged plain-PEM behavior). When set,
  `FileCertificateStore.Save()`/`ConvertToPem()` write `{host}.key` as an encrypted PKCS8 PEM
  (`ENCRYPTED PRIVATE KEY`) instead of plain PKCS8.
- New `Tls:PreviousPrivateKeyEncryptionPassphrase` (opt-in, default unset) — a rotation fallback. On load, an
  encrypted key that doesn't decrypt with the current passphrase is retried with the previous one before
  failing, so rotating the passphrase doesn't strand every already-encrypted key at the next restart.
- New `ICertificateStore.ReencryptPrivateKey(host)` (kept separate from `ConvertToPem`, not a rename/reuse of
  it — see design.md) + a matching opt-in dashboard action, shown whenever
  `PreviousPrivateKeyEncryptionPassphrase` is configured (its presence *is* the "a rotation is in progress"
  signal — no per-file detection needed). Lets an operator force re-encryption onto the current passphrase for
  every host without waiting for a natural renewal/conversion to do it implicitly.
- `PemCertificateLoader` gains encrypted-PEM support (decides plain vs. encrypted import from the PEM's own
  label — `ENCRYPTED PRIVATE KEY` vs `PRIVATE KEY` — never from whether encryption happens to be configured, so
  an operator-provided plain key keeps loading unchanged even after encryption is turned on).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tls-acme`: "ACME certificate acquisition" and "Provided certificate loading" both gain optional at-rest
  encryption of the written/loaded private key.
- `admin-api`: the dashboard's one mutating-action carve-out ("Certificate format conversion from the
  dashboard") gains a second, similarly-scoped action for passphrase re-encryption.

## Impact

- `src/DockYarp.Tls/TlsOptions.cs` — two new optional strings.
- `src/DockYarp.Tls/LoadedCertificatePem.cs` — `ExportPrivateKeyPem` takes the configured passphrase.
- `src/DockYarp.Tls/PemCertificateLoader.cs` — encrypted-PEM-aware import.
- `src/DockYarp.Tls/FileCertificateStore.cs`, `ICertificateStore.cs` — thread the passphrase(s) through
  `Save`/`Load`/`ConvertToPem`; new `ReencryptPrivateKey`.
- `src/DockYarp.AdminApi/ICertificateConverter.cs`, `src/DockYarp.App/Observability/CertificateConverterAdapter.cs`
  — new method mirroring the existing pair.
- `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`/`.cshtml.cs` — new re-encrypt action/handler,
  gated on `PreviousPrivateKeyEncryptionPassphrase` being configured.
- `tests/DockYarp.Tls.Tests/`, `tests/DockYarp.IntegrationTests/` — round-trip (encrypt/decrypt), rotation
  fallback, plain-key backward-compatibility, and the new dashboard action's tests.
- `docs-site/content/en/docs/configuration.md` — two new option rows.
