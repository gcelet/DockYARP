## Why

Loading a mounted PEM certificate whose `.crt` file contains a **full chain** (leaf + intermediate concatenated
— the standard shape produced by nginx-proxy/acme-companion, acme.sh, and virtually every real ACME client)
**silently drops the intermediate certificate**. `PemCertificateLoader.TryLoad` calls
`X509Certificate2.CreateFromPem(certPem, keyPem)`, which Microsoft's own documentation confirms loads only "the
first certificate with a CERTIFICATE label" — any further certificate blocks in the same file are silently
ignored. Only the leaf is then served during the TLS handshake, so any client that doesn't already trust the
issuing CA's intermediate out of band fails with "unable to get local issuer certificate" (curl) or an
unbypassable HSTS security error (Firefox). Reproduced live against a real deployment: `curl -vvvv` shows
exactly one `Certificate (11)` handshake message before an `unknown CA` alert.

This is general, not migration-specific: `PemCertificateLoader` is DockYARP's single mechanism for loading any
`<host>.crt`/`<host>.key` pair from the certificate directory, regardless of source (a migration, a manually-run
`certbot`/`acme.sh`, a hand-copied certificate). It breaks TLS for any host whose certificate chain isn't a
single self-contained leaf — in practice, nearly all real ACME-issued certificates.

## What Changes

- `PemCertificateLoader.TryLoad` no longer discards certificates beyond the first one in a `.crt` file. It
  parses every certificate block, attaches the private key to the one it actually matches (not assumed to be
  "whichever is first"), and builds a chain-inclusive certificate — mirroring the shape
  `FileCertificateStore`'s existing `.pfx` branch already loads correctly (confirmed: `CertesAcmeClient.BuildPfx`
  already builds ACME-issued certificates this way, chain included by default).
- The single-certificate case (no intermediate present) is unchanged — no regression for the common case that
  already works today.
- New test coverage with a real multi-certificate fixture (leaf + intermediate from a throwaway test CA),
  asserting the intermediate is actually present in what gets served — not just that loading doesn't throw.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `tls-acme`: "Provided certificate loading" requirement gains the chain-preservation behavior for multi-
  certificate `.crt` files.

## Impact

- `src/DockYarp.Tls/PemCertificateLoader.cs` — the fix.
- `tests/DockYarp.Tls.Tests/` — new test(s) with a real chain-bearing fixture.
- No config/API/label changes — purely a correctness fix to existing, already-documented loading behavior.
- Follow-up (not in this change's scope, noted for later): the already-archived
  `add-doc-nginx-proxy-migration-guide`'s claim that "no format conversion is needed" becomes accurate once this
  ships; no doc change needed here since the claim was already written assuming correct behavior.
