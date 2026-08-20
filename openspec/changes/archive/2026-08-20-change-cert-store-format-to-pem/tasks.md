## 1. Write path (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/FileCertificateStore.cs`: change `Save()` to write `{host}.crt` (full-chain PEM:
      leaf + all `Additional`, via `ExportCertificatePem()` joined) and `{host}.key` (private key PEM, RSA-then-EC
      detection on `certificate.Leaf` via `GetRSAPrivateKey()`/`GetECDsaPrivateKey()`, `ExportPkcs8PrivateKeyPem()`)
      instead of `{host}.pfx`. Verify: `dotnet build src/DockYarp.Tls/DockYarp.Tls.csproj` — 0 warnings, 0 errors.

## 2. Read-path precedence (AG-AT)

- [x] 2.1 `src/DockYarp.Tls/FileCertificateStore.cs`: change `Load()` so a `.crt`/`.key` pair takes precedence
      over a same-host `.pfx`, deterministically (not enumeration-order-dependent) — per design.md's decision
      (PFX into a working set, PEM directly into `certificates`, merge PFX-only hosts last). Verify: `dotnet build`
      — 0 warnings, 0 errors.

## 3. Unit tests (AG-AT)

- [x] 3.1 `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs`: `Save()` writes `{host}.crt`/`{host}.key`, not
      `{host}.pfx` — for both an RSA-keyed and an EC-keyed `LoadedCertificate` fixture (both algorithms are
      currently supported by the loaders and must both be provable on the write side too). Verify: the new tests
      pass and assert on the written file names/PEM content (not just "no exception").
- [x] 3.2 `Save()` writes a full-chain `.crt` (leaf + intermediate(s)) when `LoadedCertificate.Additional` is
      non-empty — reload via `PemCertificateLoader.TryLoad` and assert the chain round-trips intact. Verify: test
      passes, chain length matches input.
- [x] 3.3 `Load()` precedence: a host with both `{host}.pfx` and `{host}.crt`/`{host}.key` on disk loads the PEM
      pair's certificate, not the PFX's — construct both with distinguishable content (e.g. different serial
      numbers) so the assertion proves *which* one won, not just that loading succeeded. Verify: test passes.
- [x] 3.4 `Load()` regression: a host with only a legacy `.pfx` (no PEM pair) still loads correctly — proves no
      forced migration / no regression for existing deployments. Verify: existing-shape test passes unchanged in
      intent, updated only if the current test needs adjusting for the new merge logic. (Existing `PfxFileIsLoaded`
      test already covers this shape — verified it still passes unchanged.)
- [x] 3.5 Full `dotnet test tests/DockYarp.Tls.Tests/DockYarp.Tls.Tests.csproj` — 82/82 green (78 existing + 4
      new), no regressions in the surrounding suite.

## 4. E2E coverage (AG-AT)

- [x] 4.1 Confirmed the existing e2e suite's ACME/PEM scenarios (`TlsTests.cs`,
      `AcmeCertificate_ChainIncludesIntermediate` and neighbors) still pass now that DockYarp itself persists
      ACME-issued certs as PEM rather than PFX — full `./build.ps1 E2E` run, 32/32 green.
- [x] 4.2 `RestartPersistenceTests.ProvisionedCertificate_IsReusedAfterRestart` passed in the same run — same
      thumbprint before/after a real container restart, proving the PEM-persisted certificate is reloaded from
      disk rather than re-provisioned, the same guarantee already proven for the old PFX format. No changes
      needed to the test itself (it drives entirely over HTTPS, agnostic of the on-disk format).

## 5. Docs sweep (AG-DOC)

- [x] 5.1 Grepped `docs-site/`, `docs/*.md`, `README.md`, `docs/labels-reference.md` for `.pfx`/PFX references.
      `docs-site/content/en/docs/features.md`, `docs/deployment.md` (Data Protection key-ring encryption —
      unrelated feature), `docs/labels-reference.md`, and `README.md` were already accurate (describe the
      still-fully-supported operator-provided PEM/PFX read path, not the store's write format). Updated
      `docs/tls-acme.md`: the component table, the `CertificateDirectory` config row, and the "Provided
      certificates" section now state PEM is the persisted/canonical format and PFX is read for backward
      compatibility only, with PEM taking precedence for the same host.

## 6. Final validation (AG-AT)

- [x] 6.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors (whole solution, not just the touched project).
- [x] 6.2 `dotnet test DockYarp.slnx` — 402 unit/integration tests green (41 Core + 136 Docker + 50 Security +
      93 IntegrationTests + 82 Tls), plus the 32/32 e2e run from task 4.1/4.2.
