## Why

`fix-pem-cert-chain-dropped-on-load` (archived 2026-08-17) fixed `PemCertificateLoader` so it no longer drops
the intermediate certificate when parsing a multi-cert `.crt` file. That fix is real and necessary, but proved
insufficient on its own — verified live against a real deployment (real step-ca certificate, `.crt` confirmed
to contain both the leaf and intermediate PEM blocks): the TLS handshake still sends only the leaf. The exact
fix commit was confirmed running (cross-checked `/api/version` against local `dotnet gitversion`), so this is
not a stale-build artifact.

Root cause, verified against Microsoft's own documentation: `SniTlsHandshakeCallback.BuildOptions` sets
`SslServerAuthenticationOptions.ServerCertificate` to a bare `X509Certificate2`. Per Microsoft's TLS/SSL
best-practices guidance, when the certificate is supplied this way (rather than via `ServerCertificateContext`),
`SslStream` builds its **own** internal chain using system-store-dependent logic — not necessarily whatever
additional certificates happen to be bagged alongside the leaf. The documented, recommended approach is to
build an explicit `SslStreamCertificateContext` with `additionalCertificates` populated.

**Broader than the original stub anticipated, confirmed by code inspection (not speculation) while designing
this fix**: the same "single-certificate `LoadPkcs12`, additional certs silently dropped" pattern exists at
**three** separate call sites, not one:
1. `PemCertificateLoader.TryLoad` (fixed for parsing, but its final round-trip still calls single-cert
   `X509CertificateLoader.LoadPkcs12`, which the caller receives as a bare `X509Certificate2` with no channel
   to also carry the additional certificates it just correctly parsed).
2. `FileCertificateStore.Load()`'s `.pfx` branch — an operator-provided `.pfx` file with a bagged chain would
   hit the identical problem via single-cert `LoadPkcs12`.
3. `CertesAcmeClient.RequestCertificateAsync` (`CertesAcmeClient.cs:42`) — ACME-issued certificates are **also**
   loaded via single-cert `LoadPkcs12` immediately after `BuildPfx` constructs a chain-inclusive PFX, so the
   chain is dropped at this point too, before the certificate ever reaches `ICertificateStore.Save`. This means
   ACME-issued certificates likely have the exact same live-handshake bug as PEM-provided ones — previously
   only suspected, now confirmed by code inspection.

## What Changes

- New `LoadedCertificate` record (`X509Certificate2 Leaf`, `IReadOnlyList<X509Certificate2> Additional`) as the
  common shape for "a certificate plus whatever chain certificates travel with it," used consistently from
  loading through storage through serving.
- `X509CertificateLoader.LoadPkcs12Collection` (loads *every* certificate in a PKCS12, not just one) replaces
  single-cert `LoadPkcs12` at all three call sites identified above; the leaf is identified by `HasPrivateKey`,
  not by position.
- `ICertificateStore.Find`/`Save` and `IAcmeClient.RequestCertificateAsync` change from bare `X509Certificate2`
  to `LoadedCertificate`, so the additional certificates travel all the way from load/issuance through to disk
  persistence and back.
- `SniCertificateSelector.Select` returns `LoadedCertificate`; `SniTlsHandshakeCallback.BuildOptions` sets
  `SslServerAuthenticationOptions.ServerCertificateContext` (built via `SslStreamCertificateContext.Create`)
  instead of the bare `ServerCertificate` property.
- `DefaultCertificateProvider`'s fallback certificate follows the same shape (empty `Additional` for the
  self-signed case; non-empty if an operator supplies a chain-bearing `default.crt`/`default.key`).
- **Real e2e wire-level test coverage** (a required deliverable, not optional): the existing e2e TLS harness
  (`TlsHarness.cs`) discards the `X509Chain` its `RemoteCertificateValidationCallback` receives and accepts
  everything — which is why no existing test caught this. Extend it to capture the chain and validate against a
  client trusting only the CA root, proving the server actually sent the intermediate.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `tls-acme`: "Provided certificate loading" requirement extended — the chain preserved on load (already
  specified by `fix-pem-cert-chain-dropped-on-load`) must also be **served** during the TLS handshake, for both
  operator-provided and ACME-issued certificates.

## Impact

- `src/DockYarp.Tls/LoadedCertificate.cs` (new).
- `src/DockYarp.Tls/PemCertificateLoader.cs`, `FileCertificateStore.cs`, `CertesAcmeClient.cs`,
  `ICertificateStore.cs`, `IAcmeClient.cs`, `SniCertificateSelector.cs`, `SniTlsHandshakeCallback.cs`,
  `DefaultCertificateProvider.cs`, `CertificateProvisioningService.cs` (call-site update for the new `Save`
  signature).
- `tests/DockYarp.Tls.Tests/` — updated unit tests for the new shapes (including
  `CertificateStoreTests`, which already has the real 2-cert `TestChainFactory` fixture from the previous
  change to reuse).
- `tests/DockYarp.E2E.Tests/TlsHarness.cs` — chain-capturing update; new/extended e2e test(s) proving live
  handshake chain transmission for both a PEM-provided and an ACME-issued certificate.
