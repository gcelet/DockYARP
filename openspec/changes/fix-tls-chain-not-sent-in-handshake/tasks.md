## 1. `LoadedCertificate` shape and loading (AG-AT)

- [x] 1.1 New `src/DockYarp.Tls/LoadedCertificate.cs`: `public sealed record LoadedCertificate(X509Certificate2 Leaf, IReadOnlyList<X509Certificate2> Additional)`.
- [x] 1.2 `src/DockYarp.Tls/PemCertificateLoader.cs`: change `TryLoad` to build the chain-inclusive PKCS12 as
      today, then load it back via `X509CertificateLoader.LoadPkcs12Collection` instead of `LoadPkcs12`;
      partition the result by `HasPrivateKey` into `LoadedCertificate`. Update the `out` parameter type.
- [x] 1.3 `src/DockYarp.Tls/FileCertificateStore.cs`: `.pfx` branch switches to `LoadPkcs12Collection` +
      partition by `HasPrivateKey` (an operator-provided `.pfx` with a bagged chain was silently losing it the
      same way `.crt` was). `.crt` branch consumes `PemCertificateLoader`'s new `LoadedCertificate` return.
      `certificates` dictionary becomes `Dictionary<string, LoadedCertificate>`.

## 2. Store and ACME interfaces (AG-AT)

- [x] 2.1 `src/DockYarp.Tls/ICertificateStore.cs`: `Find(string host)` returns `LoadedCertificate?`;
      `Save(string host, LoadedCertificate certificate)` persists the full collection (`Export` via
      `X509Certificate2Collection`, not a single cert's `Export`).
- [x] 2.2 `src/DockYarp.Tls/IAcmeClient.cs`: `RequestCertificateAsync` returns `Task<LoadedCertificate>`.
- [x] 2.3 `src/DockYarp.Tls/CertesAcmeClient.cs`: after `BuildPfx`, switch from single-cert `LoadPkcs12` to
      `LoadPkcs12Collection` + partition, matching task 1.2/1.3's pattern — this is the fix for the
      independently-confirmed ACME-issued-certificate chain loss.
- [x] 2.4 `src/DockYarp.Tls/CertificateProvisioningService.cs`: update the `certificates.Save(desired.Host,
      certificate)` call site for the new `LoadedCertificate`-typed result from `RequestCertificateAsync`.

## 3. Serving the chain (AG-AT)

- [x] 3.1 `src/DockYarp.Tls/SniCertificateSelector.cs`: `Select(string? host)` returns `LoadedCertificate`
      (fallback path wraps `DefaultCertificateProvider`'s certificate with an empty `Additional`).
- [x] 3.2 `src/DockYarp.Tls/DefaultCertificateProvider.cs`: `Certificate` property becomes `LoadedCertificate`
      (empty `Additional` for the self-signed case; preserves whatever an operator-supplied `default.crt`/
      `default.key` chain carries, via task 1.2's updated `PemCertificateLoader`).
- [x] 3.3 `src/DockYarp.Tls/SniTlsHandshakeCallback.cs:110`: replace `ServerCertificate = selector.Select(host)`
      with `ServerCertificateContext = SslStreamCertificateContext.Create(selected.Leaf,
      additionalCertificates: BuildCollection(selected.Additional))`. Verify against the target framework
      whether `Create` needs an explicit flag to avoid its own AIA/system-store fetch attempts (checked at
      design time as a known open point, confirm before finalizing).
      Resolved: `offline: true` — confirmed via Microsoft docs that `false` allows network downloads of missing
      certificates during context creation; `true` restricts lookup to local certificate stores. Since the full
      chain is always supplied explicitly, offline mode keeps the handshake deterministic and network-free.

## 4. Unit tests (AG-AT)

- [x] 4.1 Update `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs` for the `LoadedCertificate` return shape
      (the existing `TestChainFactory`-based tests from `fix-pem-cert-chain-dropped-on-load` adapt directly —
      assert on `loaded.Leaf`/`loaded.Additional.Count` instead of a bare certificate).
- [x] 4.2 Update/add unit tests for `SniCertificateSelector`, `DefaultCertificateProvider`, and any
      `CertesAcmeClient`/`CertificateProvisioningService` tests touching the changed signatures.
- [x] 4.3 Full local `dotnet test` non-E2E run green before moving to e2e coverage.

## 5. Real wire-level e2e coverage — required (AG-AT)

- [x] 5.1 `tests/DockYarp.E2E.Tests/TlsHarness.cs`: stop discarding the `chain` parameter in
      `RemoteCertificateValidationCallback` — capture it (or at least `chain.ChainElements.Count` /
      `chain.ChainStatus`) on `ServerCertificateHolder` or a new holder type.
- [x] 5.2 Configure the e2e test client's `CertificateChainPolicy` with `TrustMode = CustomRootTrust` and
      *only* the step-ca root in `CustomTrustStore` (revocation off) for the specific test(s) added here, so
      the client cannot "cheat" by already trusting the intermediate from elsewhere.
- [x] 5.3 New or extended e2e test (in `TlsTests.cs` or a new file) asserting the chain actually contains the
      intermediate for a real step-ca-issued (ACME) certificate — this is the test that would have failed
      against the pre-fix code.
- [x] 5.4 If PEM-provided (mounted `.crt`/`.key`) coverage doesn't already exist at e2e level, add or extend a
      scenario for it too — both provided and ACME-issued paths were independently confirmed affected, both
      need proof.
      Added: a new `pem.local` backend (`BackendCatalog.cs`, no `LETSENCRYPT_HOST`) plus
      `TlsHarness.PrepareMountedChain()`, which writes a throwaway leaf+intermediate chain into the mounted
      certs directory before DockYarp starts, and `MountedPemCertificate_ChainIncludesIntermediate` in
      `TlsTests.cs`.
- [x] 5.5 Full `E2E` gate run (`./build.ps1 E2E` or equivalent) green, including this change's new test(s).
      Verified: `./build.ps1 E2E` — Passed: 32, Failed: 0, Skipped: 0 (DockYarp.E2E.Tests.dll), including the two
      new chain-verification tests.

## 6. Spec sync prep (AG-AT)

- [x] 6.1 Verify the delta spec's MODIFIED requirements ("Provided certificate loading", "ACME certificate
      acquisition") match what actually shipped in sections 1-5 before archiving.
      Verified: both new scenarios ("The chain is actually sent during the handshake", "An ACME-issued
      intermediate is sent during the handshake") match the implementation (`SslStreamCertificateContext`,
      `LoadedCertificate` end to end) and are each backed by a real wire-level e2e test
      (`AcmeCertificate_ChainIncludesIntermediate`, `MountedPemCertificate_ChainIncludesIntermediate`).
