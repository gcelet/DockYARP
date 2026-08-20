## Why

An operator currently has no way to retrieve a certificate DockYarp holds — ACME-issued or operator-provided —
without `docker exec`/volume access to the host. The user wants this exposed from the admin dashboard, both the
public certificate and the private key. Resolved explicitly with the user (see design.md): today's
network-isolation-only trust boundary (`AdminApi:Host`) is accepted as sufficient — no new authentication layer
is built here — but the feature ships **opt-in, disabled by default**, with a clear security warning in the
docs, since it's the dashboard's first payload with material more sensitive than status/expiry info.

## What Changes

- New `AdminApiOptions.AllowCertificateDownload` (bool, default `false`) — opt-in, mirroring the existing
  `LetsEncrypt` bool's pattern on the same options type. When `false` (default), no download route is mapped at
  all — same "don't map what's disabled" pattern the dashboard itself already follows for `Surface`.
- Two new dashboard-scoped routes (same trust boundary as `/dashboard` itself — `AdminApi:Host`-scoped, no API
  key, never routed through `/api/*`): download a host's certificate (`{host}.crt`, PEM, leaf + chain) and its
  private key (`{host}.key`, PEM) as file attachments.
- `/dashboard`'s certificate table gains download links per host, rendered only when
  `AllowCertificateDownload` is `true`.
- New `ICertificateExporter` interface (`DockYarp.AdminApi`) — kept **separate** from `ICertificateInventory`
  (which is explicitly documented "no private keys"; extending it would blur that contract) — backed by a new
  `CertificateExporterAdapter` (`DockYarp.App`) over the existing `ICertificateStore`, mirroring
  `CertificateInventoryAdapter`'s adapter pattern.
- Small refactor: `FileCertificateStore`'s private PEM-building helpers (added in `change-cert-store-format-to-pem`)
  become public extension methods on `LoadedCertificate` (`DockYarp.Tls`) so the new App-level adapter reuses
  them instead of duplicating PEM-assembly logic — justified now that a second real call site exists.
- Docs: a new `AdminApi:AllowCertificateDownload` row in the configuration reference, plus an explicit security
  warning callout (opt-in, private key exposure, network-isolation trust model) near the dashboard docs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `admin-api`: adds a new requirement for certificate download from the dashboard (opt-in, disabled by
  default, same host-scoping as the rest of the admin surface); the existing "Read-only admin dashboard"
  requirement's "read-only" framing is unaffected (downloading is not a mutation), so it is not itself modified.

## Impact

- `src/DockYarp.AdminApi/AdminApiOptions.cs` — new `AllowCertificateDownload` bool.
- `src/DockYarp.AdminApi/ICertificateExporter.cs` (new).
- `src/DockYarp.App/Observability/CertificateExporterAdapter.cs` (new) +
  `ObservabilityServiceCollectionExtensions.cs` (DI registration).
- `src/DockYarp.Tls/FileCertificateStore.cs` — PEM-building helpers extracted to a new public extension type.
- `src/DockYarp.Dashboard/DashboardEndpointMapping.cs` — two new conditional routes.
- `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`/`.cshtml.cs` — download links, gated on the new option.
- `tests/DockYarp.IntegrationTests/AdminObservabilityTests.cs` (existing file — admin/dashboard HTTP surface
  coverage lives here today, no dedicated `DockYarp.AdminApi.Tests`/`DockYarp.Dashboard.Tests` project exists)
  — coverage for the new endpoints, opt-out-by-default, and host-scoping.
- `docs-site/content/en/docs/configuration.md` — new option row + security warning.
