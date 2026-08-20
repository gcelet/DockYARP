## Why

`change-cert-store-format-to-pem` made PEM the canonical persisted format, but a certificate that hasn't been
re-provisioned/renewed since that change still sits on disk as `.pfx` — harmless (`FileCertificateStore.Load()`
reads either format), but a real, concrete consistency itch for an operator who ends up with a mixed set (e.g.
16 hosts already `.crt`/`.key`, 1 still `.pfx`). The workaround (delete the `.pfx`, force re-provisioning) works
but means waiting out a real ACME round-trip for a certificate that's already valid and already loaded. The
user wants a one-click dashboard action instead.

## What Changes

- New `AdminApiOptions.AllowCertificateConversion` (bool, default `false`) — its own opt-in, deliberately
  separate from `AllowCertificateDownload`: this is the **first mutating action** ever added to the admin
  surface, and the user explicitly chose a dedicated gate for it on that basis, not because it shares
  download's risk (it doesn't expose any new secret material — the same key was already reachable via volume
  access either way).
- `ICertificateStore` (`DockYarp.Tls`) gains `IsPfxBacked(string host)` (a live filesystem check, no new
  in-memory tracking) and `ConvertToPem(string host)` (writes the already-loaded certificate as PEM, deletes
  the stale `.pfx` — no re-provisioning, no ACME round-trip, no change to the served certificate).
- New `ICertificateConverter` interface (`DockYarp.AdminApi`) + `CertificateConverterAdapter`
  (`DockYarp.App`), mirroring `ICertificateExporter`/`CertificateExporterAdapter`'s adapter pattern.
- `/dashboard`'s certificate table gains a "Convert to PEM" action, rendered only for `.pfx`-backed hosts and
  only when `AllowCertificateConversion` is `true`. Implemented as a Razor Pages `OnPost` handler (not a GET
  minimal-API route like the download feature) specifically so the built-in anti-forgery token protects a
  state-changing action — a GET link would be CSRF-able (e.g. an `<img>` tag on an unrelated page triggering
  the mutation for anyone with dashboard network access).
- The "Read-only admin dashboard" requirement gets a small MODIFIED note carving out this one narrow,
  explicitly-opt-in exception, rather than silently becoming inaccurate.
- Docs: new `AdminApi:AllowCertificateConversion` row + a short explanation of why the workaround
  (delete-and-reprovision) still exists as the zero-config path.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `admin-api`: adds a requirement for the opt-in conversion action; the existing "Read-only admin dashboard"
  requirement's framing gets a narrow, explicit carve-out for this one action.

## Impact

- `src/DockYarp.Tls/ICertificateStore.cs`, `FileCertificateStore.cs` — two new members.
- `src/DockYarp.AdminApi/ICertificateConverter.cs`, `AdminApiOptions.cs` — new interface + new option.
- `src/DockYarp.App/Observability/CertificateConverterAdapter.cs`,
  `ObservabilityServiceCollectionExtensions.cs` — new adapter + DI registration.
- `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml`/`.cshtml.cs` — new `OnPostConvertAsync` handler, new UI
  action, per-row `IsPfxBacked` flag.
- `tests/DockYarp.Tls.Tests/`, `tests/DockYarp.IntegrationTests/AdminObservabilityTests.cs` — coverage for
  `ConvertToPem`/`IsPfxBacked`, the opt-in default, and the POST handler's behavior.
- `docs-site/content/en/docs/configuration.md` — new option row.
