## 1. Reserved-hosts seam (AG-AT)
- [x] 1.1 `DockYarp.Tls`: add `IReservedCertificateHosts` (`IReadOnlyList<DesiredCertificate> Reserved`) + a no-op
  `NoReservedCertificateHosts` default
- [x] 1.2 `TlsDomains`: add pure `Desired(RouteConfigSnapshot, IReadOnlyList<DesiredCertificate> reserved)` overload
  merging route + reserved hosts, deduped by host (OrdinalIgnoreCase; route wins on clash)
- [x] 1.3 `CertificateProvisioningService`: inject `IReservedCertificateHosts`, reconcile the merged set
- [x] 1.4 `AddDockYarpTls`: `TryAddSingleton<IReservedCertificateHosts, NoReservedCertificateHosts>()`

## 2. Admin opt-in (AG-AA)
- [x] 2.1 `AdminApiOptions`: add `LetsEncrypt` (bool, default false) + `ContactEmail` (`string?`) with XML docs
- [x] 2.2 `DockYarp.App`: `AdminReservedCertificateHosts : IReservedCertificateHosts` (contributes `Host` only when
  `LetsEncrypt` is true; email falls back to `Tls:ContactEmail`)
- [x] 2.3 `Program.cs` / observability registration: register the adapter as `IReservedCertificateHosts` (in
  `AddDockYarpObservability`, after `AddDockYarpTls` — last registration wins over the `TryAdd` default)

## 3. Tests
- [x] 3.1 `DockYarp.Tls.Tests`: `Desired(snapshot, reserved)` merge/dedup (append new, no duplicate on route clash,
  empty reserved == route-only)
- [x] 3.2 `DockYarp.IntegrationTests`: resolved `IReservedCertificateHosts.Reserved` contains the admin host with
  `AdminApi:Host` + `AdminApi:LetsEncrypt=true`; empty without the opt-in (or without `Host`)

## 4. Docs (AG-DOC — new app-config keys)
- [x] 4.1 docs site `configuration.md`: document `AdminApi:LetsEncrypt` + `AdminApi:ContactEmail`
- [x] 4.2 `examples.md`: recipe for an ACME-secured dedicated admin host

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green (unit + integration), warnings-as-errors clean
