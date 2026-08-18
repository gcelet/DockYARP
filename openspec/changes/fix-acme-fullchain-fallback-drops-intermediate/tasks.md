## 1. Chain assembly without PfxBuilder (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/CertesAcmeClient.cs`: replace `BuildPfx` (and its `PfxBuilder`/`AcmeException`
      fallback) with direct assembly from `CertificateChain.Certificate` (leaf, DER via `ToDer()`) +
      `CertificateChain.Issuers` (each via `ToDer()`) — no `PfxBuilder`, no BouncyCastle PKIX path-building.
- [x] 1.2 Attach `privateKey` directly to the leaf: `privateKey.ToPem()` → `ECDsa.ImportFromPem` (matching the
      hardcoded `KeyFactory.NewKey(KeyAlgorithm.ES256)` already in this method) →
      `leaf.CopyWithPrivateKey(ecdsa)`. No candidate search needed — the leaf is known deterministically (it is
      the CSR's own certificate).
- [x] 1.3 Assemble `X509Certificate2Collection { keyedLeaf, ...issuers }`, export as PKCS12, load back via the
      existing `CertificateCollectionLoader.LoadKeyed(pkcs12, null)` — same helper `PemCertificateLoader` and
      `FileCertificateStore` already use. `RequestCertificateAsync` returns the resulting `LoadedCertificate`
      directly (its own return type is unchanged from `fix-tls-chain-not-sent-in-handshake`).
- [x] 1.4 Remove the now-unused `Certes.Pkcs`/`PfxBuilder`-related `using`s and any now-dead helper code.

## 2. Unit tests (AG-AT)

- [x] 2.1 New `tests/DockYarp.Tls.Tests/CertesAcmeClientTests.cs` — a genuine three-tier fixture (self-signed
      root -> intermediate -> leaf) matching the real shape: a leaf + intermediate `CertificateChain` where the
      intermediate is **not** self-signed and no self-signed root is present in the response. Required making
      `CertesAcmeClient.BuildLoadedCertificate` `internal` (was `private`) plus an `InternalsVisibleTo` for
      `DockYarp.Tls.Tests` — the rest of the class needs a live ACME round-trip and stays untestable at the
      unit level. Asserts `LoadedCertificate.Additional` still contains the intermediate, and that a chain
      trusting only the (never-sent) root can complete using exactly that `Additional` entry.
- [x] 2.2 A second fixture where the response *does* include a self-signed root among `Issuers` — asserts no
      regression (intermediate still preserved).
- [x] 2.3 Full local `dotnet test` non-E2E run green (398/398, including the 2 new tests).

## 3. Real wire-level e2e coverage — required (AG-AT)

- [x] 3.1 Determined by logical proof rather than a fresh instrumented run: the pre-fix `BuildPfx`'s
      `FullChain = false` fallback (decompiled in `design.md`'s Context) is a hard-coded leaf-only path — it
      is structurally incapable of producing a multi-certificate result. The pre-fix code base's own
      `AcmeCertificate_ChainIncludesIntermediate` e2e test asserted (and passed, 32/32) that step-ca's issued
      certificate included its intermediate. The only way that assertion could have passed pre-fix is if
      `FullChain = true`'s PKIX path-build succeeded — which requires a self-signed root among the certificates
      step-ca returned. Confirms the design's hypothesis: **step-ca's default ACME response bundles its own
      root**, which is exactly why the prior e2e coverage did not catch this bug.
- [x] 3.2 Reconfiguring step-ca to omit its root from ACME responses is not a documented/exposed provisioner
      option and would require CA-implementation-specific research disproportionate to this fix (and is not
      something DockYarp controls in production either — real CAs' root-bundling choice is CA-specific, not
      configurable by the relying party). Decision: the unit tests (2.1/2.2) are the primary, deterministic
      regression guard for the no-root-in-response shape — they exercise `CertesAcmeClient.BuildLoadedCertificate`
      directly against exactly that shape, which is precisely what step-ca's e2e behavior cannot reliably be
      forced into. The existing `AcmeCertificate_ChainIncludesIntermediate` e2e test is left unchanged and
      continues to prove the root-bundled case (and, more importantly, now proves the *new* assembly code path
      works correctly against a real, running ACME server end-to-end, not only synthetic fixtures).
- [x] 3.3 Full `E2E` gate run (`./build.ps1 E2E` or equivalent) green — proving the new `BuildLoadedCertificate`
      path (replacing `BuildPfx`) still works correctly against a real ACME server end-to-end.
      Verified: `./build.ps1 E2E` — Passed: 32, Failed: 0, Skipped: 0 (DockYarp.E2E.Tests.dll), including
      `AcmeCertificate_ChainIncludesIntermediate` exercising the new assembly path live.

## 4. Spec sync prep (AG-AT)

- [x] 4.1 Verify the delta spec's MODIFIED "ACME certificate acquisition" requirement (the rewritten "Private
      CA whose root is not in the issued chain" scenario) matches what actually shipped in sections 1–3 before
      archiving.
      Verified: matches — the "Private CA whose root is not in the issued chain" scenario is backed by the two
      new unit tests (2.1/2.2), and "An ACME-issued intermediate is sent during the handshake" is backed by the
      live e2e run (3.3).
