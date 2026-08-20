## 1. Store-level conversion (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/ICertificateStore.cs`: added `bool IsPfxBacked(string host)` and `bool
      ConvertToPem(string host)` to the interface, XML-documented. Verify: `dotnet build src/DockYarp.Tls` —
      0 warnings/errors.
- [x] 1.2 `src/DockYarp.Tls/FileCertificateStore.cs`: implemented `IsPfxBacked` as a live filesystem check
      (`.pfx` exists, `.crt` doesn't). Implemented `ConvertToPem` under the existing `gate` lock: `Find(host)`,
      write `{host}.crt`/`{host}.key` via the `LoadedCertificatePem` extension methods, delete `{host}.pfx` if
      present, return `false` if the host isn't found. Does **not** call `Save()` (see design.md's dispose-trap
      rationale). Verify: `dotnet build` — 0 warnings/errors.
- [x] 1.3 `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs`: `IsPfxBacked` true/false/false (pfx-only,
      PEM-pair, unknown). `ConvertToPem` on a `.pfx`-backed host writes the PEM pair, deletes the `.pfx`, and
      the object returned by `Find` afterward is **reference-equal** to the pre-conversion one (airtight proof
      `Save()`'s dispose path was never touched). `ConvertToPem` on an unknown host returns `false`, touches no
      files. **Real bug found and fixed while writing this test, not assumed**:
      `ConvertToPemRewritesPfxAsUsablePem` initially failed with `CryptographicException: The requested
      operation is not supported` — a certificate loaded from `.pfx` via
      `X509CertificateLoader.LoadPkcs12Collection` imports its private key into a non-exportable key store
      (CNG on Windows) by default, so `ExportPkcs8PrivateKeyPem()` on it always fails, regardless of retrying.
      This was a latent gap in `CertificateCollectionLoader.LoadKeyed` since before this session — nothing had
      ever needed to re-export a PFX-loaded key before `ConvertToPem`. Fixed by passing
      `X509KeyStorageFlags.Exportable` at import time (confirmed via `microsoft_docs_search` before using it, not
      guessed). Verify: `dotnet test tests/DockYarp.Tls.Tests` — 85/85 green (82 prior + 3 new).

## 2. Admin surface plumbing (AG-AA)

- [x] 2.1 `src/DockYarp.AdminApi/ICertificateConverter.cs` (new): mirrors `ICertificateExporter`'s shape —
      `IsPfxBacked(string host)` and `ConvertToPem(string host)`. Verify: `dotnet build src/DockYarp.AdminApi`
      — 0 warnings/errors.
- [x] 2.2 `src/DockYarp.App/Observability/CertificateConverterAdapter.cs` (new): implements
      `ICertificateConverter` over `ICertificateStore`. Verify: `dotnet build src/DockYarp.App` —
      0 warnings/errors.
- [x] 2.3 `src/DockYarp.AdminApi/AdminApiOptions.cs`: new `AllowCertificateConversion` bool, default `false`,
      XML-documented as its own opt-in (not reusing `AllowCertificateDownload`), explaining why (first mutating
      action, gated independently of secret-exposure risk). Verify: `dotnet build` — 0 warnings/errors.
- [x] 2.4 `src/DockYarp.App/Observability/ObservabilityServiceCollectionExtensions.cs`: registered
      `services.AddSingleton<ICertificateConverter, CertificateConverterAdapter>()`. Verify: covered by task 4's
      integration tests (DI resolves correctly).

## 3. Dashboard UI + handler (AG-AA)

- [x] 3.1 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml.cs`: `IndexModel` takes `ICertificateConverter`;
      `CertificateRow` gains `IsPfxBacked`; `AllowCertificateConversion` exposed alongside the existing
      `AllowCertificateDownload` property. New `OnPostConvert(string host)` handler (not `-Async`: no actual
      async work): no-op when `AllowCertificateConversion` is `false` (defense in depth, not just UI-gated);
      otherwise calls `certificateConverter.ConvertToPem(host)` and redirects back to `/dashboard`
      (post-redirect-get). Verify: `dotnet build` — 0 warnings/errors.
- [x] 3.2 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`: added a "Convert to PEM" form/button per
      `.pfx`-backed row, `<form method="post" asp-page-handler="Convert" asp-route-host="@cert.Host">`, rendered
      only when `Model.AllowCertificateConversion && cert.IsPfxBacked`. **Unplanned but required addition**:
      `src/DockYarp.Dashboard/Pages/_ViewImports.cshtml` did not exist — without `@addTagHelper *,
      Microsoft.AspNetCore.Mvc.TagHelpers`, `asp-page-handler`/`asp-route-host` render as inert literal
      attributes and, critically, the form tag helper never auto-injects an anti-forgery token — silently
      defeating the entire CSRF protection this feature's design depends on. Added it. Verify: `dotnet build
      DockYarp.slnx` — 0 warnings/errors (functional coverage, including that the token is actually present, in
      task 4).

## 4. Integration tests (AG-AA)

- [x] 4.1 `CertificateConversionDefaultsToDisabled` — with `AllowCertificateConversion` unset (`false`), the
      rendered `/dashboard` HTML contains no "Convert to PEM" form at all for a host the fake converter reports
      as PFX-backed, and the converter is never invoked. (Redesigned from the original plan of POSTing with a
      valid token while disabled: with the feature off, the page never emits an anti-forgery token in the first
      place — asserting the UI never offers the action is the meaningful, reachable check here.)
- [x] 4.2 `ConvertingCertificateInvokesConverter` — with `AllowCertificateConversion = true`: fetches the real
      anti-forgery token from the rendered page, posts the convert handler for `app.local`, gets a redirect back
      to `/dashboard` (`AllowAutoRedirect = false` on the test client so the raw 302 is observable), and the fake
      converter recorded exactly that one host.
- [x] 4.3 `ConvertingWithoutAntiForgeryTokenIsRejected` — posting without a token returns 400 and the converter
      is never invoked, proving the CSRF protection is genuinely active — confirms task 3.2's `_ViewImports.cshtml`
      addition actually works, not just that it seemed necessary in theory.
- [x] 4.4 Full `dotnet test tests/DockYarp.IntegrationTests/DockYarp.IntegrationTests.csproj` — 102/102 green
      (99 prior + 3 new).

## 5. Docs (AG-DOC)

- [x] 5.1 `docs-site/content/en/docs/configuration.md`: new `AdminApi:AllowCertificateConversion` row, default
      `false`, noting it's the one mutating dashboard action and what it actually does (rewrite on-disk format
      only, no re-provisioning).

## 6. Final validation (AG-AA)

- [x] 6.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 6.2 `dotnet test DockYarp.slnx` — 414 unit/integration tests green (41 Core + 136 Docker + 50 Security +
      102 IntegrationTests + 85 Tls), plus 32/32 e2e.
- [x] 6.3 Manual smoke test, run for real (not simulated): generated a genuine self-signed `.pfx` via a
      throwaway console app, started `DockYarp.App` locally pointed at that certs directory
      (`AllowCertificateConversion=true`), confirmed `/dashboard` rendered the real "Convert to PEM" form and
      anti-forgery token for the host, submitted it with `curl` carrying the real cookie + token, got a 302, and
      confirmed on disk: `smoketest.local.pfx` gone, `smoketest.local.crt`/`.key` present. Re-fetched `/dashboard`
      afterward — the action no longer offers itself for that host (correctly no longer PFX-backed), and the app
      stayed healthy (200) throughout.
