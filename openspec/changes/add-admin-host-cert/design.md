# Design — add-admin-host-cert (ACME for the dedicated admin host)

## The constraint
`Core` is the dependency leaf; `Tls` is a library that must **not** reference `AdminApi`. So the admin host cannot be
injected by teaching `TlsDomains`/`CertificateProvisioningService` to read `AdminApiOptions`. The seam must let a
higher layer (`App`, which references everything) contribute desired hosts to the provisioning loop through a `Tls`
abstraction.

## The seam: `IReservedCertificateHosts`
```csharp
// DockYarp.Tls
public interface IReservedCertificateHosts
{
    IReadOnlyList<DesiredCertificate> Reserved { get; }
}
```
- **Default (no-op)** registered by `AddDockYarpTls` via `TryAddSingleton<IReservedCertificateHosts, NoReservedCertificateHosts>()`
  where `Reserved => []`. So `Tls` alone provisions exactly as today.
- `App` registers `AdminReservedCertificateHosts` in `AddDockYarpObservability` (which already binds `AdminApiOptions`).
  It runs after `AddDockYarpTls`, so its plain `AddSingleton` is the **last** registration and wins over the `TryAdd`
  default on resolve. Dependencies (`AdminApiOptions`, `TlsOptions`) are resolved lazily when the background service
  first reconciles, so registration order of those does not matter.

### Merge (pure, unit-testable)
```csharp
// DockYarp.Tls.TlsDomains
public static IReadOnlyList<DesiredCertificate> Desired(
    RouteConfigSnapshot snapshot, IReadOnlyList<DesiredCertificate> reserved)
```
Returns route desires (`Desired(snapshot)`) plus every reserved host **not already** a route desire, deduped by host
with `StringComparer.OrdinalIgnoreCase`. Route desires win on a name clash (a real route already covers that host).
`CertificateProvisioningService` calls this overload with `reserved.Reserved`; everything downstream (the
`NeedsCertificate` check, ACME request, `certificates.Save`, renewal timer) is unchanged.

## The App adapter (gating)
```csharp
// DockYarp.App
internal sealed class AdminReservedCertificateHosts(AdminApiOptions admin, TlsOptions tls) : IReservedCertificateHosts
{
    public IReadOnlyList<DesiredCertificate> Reserved =>
        admin is { LetsEncrypt: true, Host: { Length: > 0 } host }
            ? [new DesiredCertificate(host, admin.ContactEmail ?? tls.ContactEmail)]
            : [];
}
```
- **Opt-in is explicit** (`AdminApi:LetsEncrypt`), mirroring `LETSENCRYPT_HOST` for routes: without it the admin host
  keeps the fallback/operator certificate (the `isolate-admin-api` MVP behavior — unchanged by default).
- Contact email falls back to `Tls:ContactEmail` exactly as routes do (`CertesAcmeClient` already applies that
  fallback and requires accepted ToS + a directory URI, so no new ACME gating is introduced here).

## Options added (bound from the `AdminApi` section)
| Key | Type | Default | Meaning |
|---|---|---|---|
| `AdminApi:LetsEncrypt` | bool | `false` | Opt in to ACME-provisioning a certificate for `AdminApi:Host`. |
| `AdminApi:ContactEmail` | string? | `null` | ACME contact for the admin host; falls back to `Tls:ContactEmail`. |

## Why no SNI / challenge change
`certificates.Save(host, cert)` keys by host and `SniCertificateSelector` does an exact `store.Find(sni)` first, so
the admin-host certificate is served on the admin SNI once present. The HTTP-01 challenge is already answered from the
token store independent of routing (see tls-acme "ACME HTTP-01 challenge serving"), so the admin host validates on the
standard edge like any other host.

## Behavior
| `AdminApi:Host` | `AdminApi:LetsEncrypt` | ToS + contact | Admin host certificate |
|---|---|---|---|
| unset | — | — | none reserved (unchanged) |
| `admin.local` | `false` | — | fallback/operator cert (isolate-admin-api MVP) |
| `admin.local` | `true` | ok | **ACME-provisioned + renewed** for `admin.local` |
| `admin.local` | `true` | missing contact | provisioning throws per host, logged, isolated (existing resilient loop) |

## Tests
- **Unit (`DockYarp.Tls.Tests`)**: `Desired(snapshot, reserved)` — a reserved host absent from routes is appended; a
  reserved host equal to a route host is not duplicated; empty reserved == `Desired(snapshot)`.
- **Integration (`DockYarp.IntegrationTests`)**: resolve `IReservedCertificateHosts` from the app; with
  `AdminApi:Host=admin.local` + `AdminApi:LetsEncrypt=true` its `Reserved` contains `admin.local`; without the opt-in
  (or without `Host`) it is empty. (No ACME round-trip — real issuance is covered by the existing e2e `TlsTests`.)

## Out of scope
- **Dedicated admin port**: a port other than 80/443 breaks HTTP-01 → separate later follow-up; this change keeps the
  admin host on the standard edge so ACME works.
