## Why
`isolate-admin-api` shipped the blocking half: `AdminApi:Host` scopes the admin API + `/metrics` to a dedicated host
so a backend's `/api/*` is no longer shadowed. But that admin host has **no certificate desire of its own**:
`CertificateProvisioningService` reconciles only `TlsDomains.Desired(snapshot)`, which derives hosts from **routes**
(`RouteConfigSnapshot`). The admin host is served by DockYarp itself, not by a proxied backend, so it is never a route
— it therefore falls back to the self-signed default certificate. The user needs a **real ACME certificate** on the
admin host to test on a Docker host that fronts many services (*"à minima hôte admin dédié afin de pouvoir avoir un
certificat ACME"*).

## What Changes
- Add a **reserved-hosts certificate-desire seam** so hosts that are not routes (starting with the admin host) can be
  provisioned, **without `Tls` depending on `AdminApi`** (the `Core`-leaf / no-cycle constraint):
  - `Tls`: `IReservedCertificateHosts` returning extra `DesiredCertificate`s, with a no-op default; a pure
    `TlsDomains.Desired(snapshot, reserved)` overload merges route + reserved hosts (deduped by host).
  - `CertificateProvisioningService` reconciles the merged set.
- Add an explicit **opt-in** for the admin host: `AdminApi:LetsEncrypt` (bool, default `false`) plus an optional
  `AdminApi:ContactEmail`. `App` registers an adapter that contributes `AdminApi:Host` as a reserved desire **only**
  when `Host` is set **and** `LetsEncrypt` is `true` (contact falls back to `Tls:ContactEmail`, as routes do).
- No SNI change: once provisioned, the admin-host certificate is stored by host and the existing SNI selector serves
  it; the HTTP-01 challenge is already answered host-agnostically.

## Capabilities
### Modified Capabilities
- `tls-acme`: certificate provisioning also covers reserved (non-route) hosts, gated by opt-in.

## Impact
- **Code**: `DockYarp.Tls` (`IReservedCertificateHosts`, no-op default, `TlsDomains.Desired` overload,
  `CertificateProvisioningService` merge, DI `TryAdd`); `DockYarp.AdminApi` (`AdminApiOptions.LetsEncrypt` +
  `ContactEmail`); `DockYarp.App` (`AdminReservedCertificateHosts` adapter + registration).
- **Tests**: `DockYarp.Tls.Tests` (merge/dedup of the `Desired` overload); `DockYarp.IntegrationTests` (the resolved
  `IReservedCertificateHosts` contains the admin host only when opted in). No e2e — provisioning against a real CA is
  already covered by `TlsTests.AcmeCertificate_IsProvisionedForHost`; the reserved-host desire is deterministic logic
  proven in unit + integration (per `docs/testing.md`).
- **Docs (user-facing — new app-config keys)**: docs site `configuration.md` (`AdminApi:LetsEncrypt`,
  `AdminApi:ContactEmail`) + an `examples.md` recipe for an ACME-secured admin host.
- **Scope**: closes the ACME half of the admin-host split. A **dedicated admin port** stays a separate later follow-up
  (a port other than 80/443 breaks HTTP-01). Owning agent: AG-AT (with AG-AA for the AdminApi options).
