## 1. Options (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/TlsOptions.cs`: new `PrivateKeyEncryptionPassphrase` and
      `PreviousPrivateKeyEncryptionPassphrase` (both `string?`, default `null`), XML-documented explaining the
      rotation-fallback relationship between them and the admin-dashboard-download non-goal. Verify: `dotnet
      build src/DockYarp.Tls` — 0 warnings/errors.

## 2. Write path (AG-AT)

- [x] 2.1 `src/DockYarp.Tls/LoadedCertificatePem.cs`: `ExportPrivateKeyPem` takes an additional `string?
      passphrase` parameter (required, no default value — the project's analyzer forbids optional params with
      a default) — plain `ExportPkcs8PrivateKeyPem()` when null/empty, else
      `ExportEncryptedPkcs8PrivateKeyPem(passphrase, pbeParameters)` with a fixed internal `PbeParameters`
      constant (AES-256-CBC, SHA-256, 600,000 iterations, OWASP-cited in a comment). Updated the one other call
      site (`CertificateExporterAdapter.Export`, the download feature) to explicitly pass `null` — the
      downloaded copy stays plain by design (see the added remarks: at-rest encryption doesn't defend that
      path anyway).
- [x] 2.2 `src/DockYarp.Tls/FileCertificateStore.cs`: `Save()` and `ConvertToPem()` pass
      `options.PrivateKeyEncryptionPassphrase` into `ExportPrivateKeyPem`. Verify: `dotnet build` — 0
      warnings/errors.

## 3. Read path — restructured for decrypt-once, fail-loud (AG-AT)

- [x] 3.1 `src/DockYarp.Tls/PemCertificateLoader.cs`: restructured so the key is decrypted/imported into an
      `RSA`/`ECDsa` instance exactly once (not once per candidate certificate): plain vs. encrypted decided from
      the PEM's own label (`ENCRYPTED PRIVATE KEY` substring); plain keeps the unchanged try-RSA-then-EC
      behavior (malformed key → null → caller skips, same as before); encrypted tries the current passphrase
      then the previous (if configured) for both algorithms, throwing an `InvalidOperationException` naming the
      key file path if all attempts fail. The per-candidate loop now only does `CopyWithPrivateKey` + catches
      `ArgumentException` (public-key mismatch), unrelated to decryption. New `PrivateKeyPassphrases.cs`
      (`internal readonly record struct` bundling current+previous) keeps `TryLoad`'s parameter count at the
      project's 5-parameter analyzer limit; threaded through both real call sites
      (`FileCertificateStore.Load()`, and `DefaultCertificateProvider.cs` — a call site not mentioned in the
      original task list, found while implementing).
- [x] 3.2 `dotnet build DockYarp.slnx` — 0 warnings/errors (whole solution; fixed 3 pre-existing direct
      `PemCertificateLoader.TryLoad` calls in `CertificateStoreTests.cs` to pass `PrivateKeyPassphrases.None`).

## 4. Regression + new unit coverage (AG-AT)

- [x] 4.1 `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs`: all pre-existing chain-preservation/reversed-order/
      mismatched-key tests still pass unmodified — the regression guard for task 3's refactor.
- [x] 4.2 New test `SaveEncryptsPrivateKeyWhenPassphraseConfigured`: `Save()` writes an `ENCRYPTED PRIVATE KEY`
      PEM when `PrivateKeyEncryptionPassphrase` is configured, and the store round-trips it (reload via a fresh
      store, same thumbprint, usable private key). Found and fixed a test bug along the way: the test read
      `certificate.Thumbprint` after the inner `using (FileCertificateStore store = ...) { store.Save(...) }`
      block had already closed — `FileCertificateStore.Dispose()` disposes every certificate it holds, including
      the exact object the test's own outer `using` also referenced, so the property read hit a disposed handle
      (`CryptographicException: m_safeCertContext is an invalid handle`). Fixed by capturing
      `certificate.Thumbprint` into a local `string` immediately after certificate creation, before any store
      could touch it, and asserting against that captured value instead of the live (by-then-disposed) object.
- [x] 4.3 New test `PlainKeyStillLoadsWhenPassphraseConfigured`: an operator-provided **plain** key still loads
      correctly when `PrivateKeyEncryptionPassphrase` is configured (the "don't decide from config" guarantee).
- [x] 4.4 New test `EncryptedKeyLoadsViaPreviousPassphraseFallback`: a key encrypted with the *previous*
      passphrase (current passphrase changed, `PreviousPrivateKeyEncryptionPassphrase` set to the old value)
      loads via the fallback.
- [x] 4.5 New test `UndecryptableEncryptedKeyFailsFast`: a key encrypted with neither the current nor previous
      configured passphrase throws a clear, actionable `InvalidOperationException` naming the key file.
- [x] 4.6 `dotnet test tests/DockYarp.Tls.Tests/DockYarp.Tls.Tests.csproj` — 89/89 passed, 0 failed.

## 5. Admin surface: re-encryption action (AG-AA)

- [x] 5.1 `src/DockYarp.Tls/ICertificateStore.cs` + `FileCertificateStore.cs`: new
      `bool ReencryptPrivateKey(string host)` — same shape as `ConvertToPem` (find the already-loaded
      certificate, write `{host}.key` under the current passphrase settings; return `false` if the host isn't
      found) but does not touch `.crt` content or any `.pfx` file. `FakeCertificateStore` (test double) updated
      to match. Verified via `dotnet build` — 0 warnings/errors (done ahead of schedule alongside task 3).
- [x] 5.2 `src/DockYarp.AdminApi/ICertificateConverter.cs` + `src/DockYarp.App/Observability/
      CertificateConverterAdapter.cs`: added `ReencryptPrivateKey(string host)` mirroring `ConvertToPem`'s
      existing pairing. Also added `PrivateKeyEncryptionConfigured` (a capability-level bool, backed by
      `TlsOptions` in the adapter) and `RequiresKeyReencryption(string host)` (per-host, backed by the store) —
      see task 5.5 for why these live on `ICertificateConverter` rather than `IndexModel` injecting `TlsOptions`
      directly. Verified: `dotnet build DockYarp.slnx` — 0 warnings/errors.
- [x] 5.3 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml.cs`: `IndexModel.AllowKeyReencryption =>
      adminApiOptions.AllowCertificateConversion && certificateConverter.PrivateKeyEncryptionConfigured` — via
      `ICertificateConverter`, not a direct `TlsOptions` dependency (task 5.5 explains why the original plan for
      this task was wrong). New `OnPostReencrypt(string host)` handler mirrors `OnPostConvert`'s defense-in-depth
      re-check + redirect-to-page shape.
- [x] 5.4 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`: new "Re-encrypt key" form/button per row,
      rendered whenever `AllowKeyReencryption` is true — for every host, not conditionally per-row (a per-row
      "needs it" indicator is added separately by task 5.6, as a badge alongside this button, not as a
      conditional on the button itself). Verified: `dotnet build DockYarp.slnx` — 0 warnings/errors.
- [x] 5.5 **Correction found live**: this task's original text (see git history) specified injecting `TlsOptions`
      directly into `IndexModel` to compute `AllowKeyReencryption`. That does not compile —
      `DockYarp.Dashboard.csproj` deliberately references only `DockYarp.AdminApi` and `DockYarp.Core`, never
      `DockYarp.Tls`, and this change should not weaken that layering. Fixed by exposing
      `ICertificateConverter.PrivateKeyEncryptionConfigured` (an `AdminApi`-level capability boolean, same
      boundary `IsPfxBacked` already crosses) instead, backed by `TlsOptions` only inside
      `CertificateConverterAdapter` (`DockYarp.App`, which already references `DockYarp.Tls`). See design.md's
      first "Decisions" note under this task's heading.
- [x] 5.6 **Addendum, requested by the user after 5.1–5.5 shipped**: a per-row "needs re-encryption" badge next
      to the "Re-encrypt key" button, so the admin can see which hosts still have a plain or previous-passphrase
      key without guessing. Required threading a new signal out of `PemCertificateLoader.TryLoad` (which
      passphrase tier — if any — decrypted the key), via a new `PemLoadResult` type (property-initialized, not
      positional, to avoid an AV1564 bare-`bool`-parameter violation); `FileCertificateStore` tracks the
      per-host flag in a `HashSet<string>`, set on `Load()`, cleared by `Save()`/`ConvertToPem()`/
      `ReencryptPrivateKey()`; exposed through `ICertificateStore.RequiresKeyReencryption(string host)` →
      `ICertificateConverter.RequiresKeyReencryption(string host)` → `IndexModel`'s `CertificateRow` → a
      `badge-warn` span in `Index.cshtml`, reusing the table's existing "near expiry" badge styling. A
      PFX-backed host is never flagged (`ConvertToPem` already applies the current passphrase when it rewrites
      the key). See design.md's second "Decisions" note under this task's heading. Verified:
      `dotnet build DockYarp.slnx` — 0 warnings/errors; 6 new unit tests in
      `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs` (95/95 passing) plus 1 new integration test in
      `tests/DockYarp.IntegrationTests/AdminObservabilityTests.cs` (107/107 passing).

## 6. Integration tests (AG-AA)

- [x] 6.1 `tests/DockYarp.IntegrationTests/AdminObservabilityTests.cs`: re-encryption action absent from the
      rendered page and rejected server-side (defense in depth, via a genuinely valid anti-forgery token) when
      no passphrase is configured, even with `AllowCertificateConversion = true`
      (`KeyReencryptionAbsentAndRejectedWhenPassphraseNotConfigured`).
- [x] 6.2 Present and invokable (valid anti-forgery token, `ICertificateConverter.ReencryptPrivateKey` called)
      when a passphrase **is** configured — covering both a fake "PFX-backed" host (`app.local`) and a fake
      "plain key" host (`plain.local`), proving it isn't restricted the way `ConvertToPem`'s own action is
      (`ReencryptingKeyInvokesConverter`, parameterized).
- [x] 6.3 Rejected without a valid anti-forgery token (`ReencryptingWithoutAntiForgeryTokenIsRejected`, mirrors
      `ConvertingWithoutAntiForgeryTokenIsRejected`).
- [x] 6.4 Full `dotnet test DockYarp.slnx` (unit + integration, e2e excluded — matches this change's approved
      test strategy) — 429/429 passing, no regressions.

## 7. Docs (AG-DOC)

- [x] 7.1 `docs-site/content/en/docs/configuration.md`: new `Tls:PrivateKeyEncryptionPassphrase` and
      `Tls:PreviousPrivateKeyEncryptionPassphrase` rows — explicitly state the admin-dashboard-download
      non-goal (this protects against volume/backup-only access, not a dashboard-adjacent attacker), matching
      the honesty standard already set for `AllowCertificateDownload`'s own security note. Also updated the
      `AdminApi:AllowCertificateConversion` row to mention it now also gates the re-encryption action, and added
      a matching paragraph to `docs-site/content/en/docs/features.md`'s "Admin dashboard" section (mirroring the
      existing `AllowCertificateDownload` paragraph's shape and its same non-goal statement).

## 8. Final validation (AG-AT)

- [x] 8.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 8.2 `dotnet test DockYarp.slnx` (unit + integration, e2e excluded per this change's approved test
      strategy) — 429/429 passing, no regressions.
- [x] 8.3 Manual smoke test, run for real against the actual `dotnet run` app (not simulated) — **used a scratch
      `Tls:CertificateDirectory` under the session scratchpad, per [[smoke-test-scratch-dirs]], never the
      default**: pre-seeded `smoke.local.crt`/`smoke.local.key` (plain PEM, via `openssl req -x509`), started
      the app with `Tls:PrivateKeyEncryptionPassphrase` set — confirmed it started, served TLS
      (`openssl s_client` handshake succeeded), and the dashboard showed the "needs re-encryption" badge for
      the plain key. Triggered the "Re-encrypt key" dashboard action via a real anti-forgery-token POST
      (`curl`) — confirmed `smoke.local.key` became `ENCRYPTED PRIVATE KEY` on disk, the badge disappeared, and
      TLS still served correctly. Restarted the app — confirmed it still started (encrypted key loaded via the
      current passphrase) and the badge stayed absent. No temp file leaked into the repo (removed the
      throwaway `appsettings.Smoke.json` copy from `src/DockYarp.App/` after the run).
