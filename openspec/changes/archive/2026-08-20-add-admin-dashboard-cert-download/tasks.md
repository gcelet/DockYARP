## 1. PEM-assembly refactor (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/LoadedCertificatePem.cs` (new): public extension methods `ExportChainPem(this
      LoadedCertificate)` and `ExportPrivateKeyPem(this LoadedCertificate)`, moved verbatim from
      `FileCertificateStore`'s private static helpers. Verify: `dotnet build src/DockYarp.Tls` — 0 warnings/errors.
- [x] 1.2 `src/DockYarp.Tls/FileCertificateStore.cs`: `Save()` calls the new extension methods instead of its
      own now-removed private helpers (also dropped the now-unused `System.Security.Cryptography` using).
      Verify: existing `DockYarp.Tls.Tests` suite still 82/82 green (no behavior change, pure extraction).

## 2. Export interface + adapter (AG-AA)

- [x] 2.1 `src/DockYarp.AdminApi/ICertificateExporter.cs` + `CertificateExport.cs` (new, split into two files
      per `MA0048`): a method to retrieve a stored certificate's chain PEM and private-key PEM for a host,
      returning `null` when the host has no certificate — kept separate from `ICertificateInventory`. Verify:
      `dotnet build src/DockYarp.AdminApi` — 0 warnings/errors.
- [x] 2.2 `src/DockYarp.App/Observability/CertificateExporterAdapter.cs` (new): implements `ICertificateExporter`
      over `ICertificateStore.Find(host)`, using the task 1.1 extension methods. Verify: `dotnet build
      src/DockYarp.App` — 0 warnings/errors.
- [x] 2.3 `src/DockYarp.App/Observability/ObservabilityServiceCollectionExtensions.cs`: register
      `services.AddSingleton<ICertificateExporter, CertificateExporterAdapter>()` alongside the existing
      `ICertificateInventory` registration. Verify: app starts locally without a DI resolution error (covered
      by task 5's integration test run, not a separate manual step).

## 3. Opt-in setting (AG-AA)

- [x] 3.1 `src/DockYarp.AdminApi/AdminApiOptions.cs`: new `AllowCertificateDownload` bool, default `false`,
      XML-documented mirroring `LetsEncrypt`'s opt-in phrasing. Verify: `dotnet build` — 0 warnings/errors.

## 4. Dashboard routes + UI (AG-AA)

- [x] 4.1 `src/DockYarp.Dashboard/DashboardEndpointMapping.cs`: inside `MapDockYarpDashboard`, when
      `options.AllowCertificateDownload` is `true` (in addition to the existing `Surface ==
      ApiAndDashboard` gate), maps `GET /dashboard/certs/{host}/certificate` and `GET
      /dashboard/certs/{host}/private-key` (`MapCertificateDownload`), each resolving `ICertificateExporter` via
      `Results.File(...)` (sets `Content-Disposition: attachment; filename={host}.crt`/`.key` automatically) or
      `Results.NotFound()` when the host has no certificate. Both scoped to the same `RequireHost` as the rest of
      the dashboard mapping. Verify: `dotnet build src/DockYarp.Dashboard` — 0 warnings/errors.
- [x] 4.2 `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml.cs`: `IndexModel` now takes `AdminApiOptions`,
      exposing `AllowCertificateDownload`. `Index.cshtml`: added a "Download" column with certificate/private-key
      links per row, rendered only when the option is `true`. Verify: `dotnet build DockYarp.slnx` — whole
      solution 0 warnings/errors (functional coverage in task 5).

## 5. Integration tests (AG-AA)

- [x] 5.1 `CertificateDownloadDefaultsToDisabled` — both download routes 404 when `AllowCertificateDownload` is
      unset, proven against an exporter that *would* match (`app.local`), so the 404 proves the route isn't
      mapped, not merely that the lookup failed.
- [x] 5.2 `DownloadingCertificateReturnsPemChain` / `DownloadingPrivateKeyReturnsPemKey` — with
      `AllowCertificateDownload = true`, both routes return 200 with the exact PEM body and the expected
      `Content-Disposition` file name (`app.local.crt`/`app.local.key`).
- [x] 5.3 `DownloadingUnknownHostReturnsNotFound` — a host with no stored certificate 404s.
- [x] 5.4 `DownloadFollowsHostIsolation` — a request with a different `Host` header than the configured admin
      host is not served by the download route (mirrors the `client.DefaultRequestHeaders.Host = "..."` pattern
      already used in `ProxyIntegrationTests`/`ForwardedHeadersIntegrationTests`).
- [x] 5.5 `DownloadNeedsNoAdminApiKey` — the download succeeds with no `X-Api-Key` header ever set on the client.
- [x] 5.6 Full `dotnet test tests/DockYarp.IntegrationTests/DockYarp.IntegrationTests.csproj` — 99/99 green
      (93 existing + 6 new), no regressions.

## 6. Docs (AG-DOC)

- [x] 6.1 `docs-site/content/en/docs/configuration.md`: new `AdminApi:AllowCertificateDownload` row in the
      AdminApi options table (default `false`), with an inline security note.
- [x] 6.2 `docs-site/content/en/docs/features.md`: added an explicit security-warning paragraph to the "Admin
      dashboard" section — private key exposed over HTTP once enabled, protected only by `AdminApi:Host`
      network isolation, no application-level auth. Names/defaults verified against the shipped code
      (`AllowCertificateDownload`, default `false`).

## 7. Final validation (AG-AA)

- [x] 7.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 7.2 `dotnet test DockYarp.slnx` — 408 unit/integration tests green (41 Core + 136 Docker + 50 Security +
      99 IntegrationTests + 82 Tls), plus 32/32 e2e.
