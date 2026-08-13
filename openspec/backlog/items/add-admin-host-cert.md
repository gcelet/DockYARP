---
id: add-admin-host-cert
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: medium
nginx-proxy: n/a (DockYarp-specific — admin host TLS)
provenance: 2026-08-13 split-off from isolate-admin-api (MVP shipped the host isolation)
status: backlog
---

## Why
`isolate-admin-api` shipped the blocking half: `AdminApi:Host` scopes the admin API + `/metrics` to a dedicated
host so a backend's `/api/*` is no longer shadowed. But that admin host has **no certificate desire** of its own —
`TlsDomains.Desired` derives hosts to provision only from **routes** (`RouteConfigSnapshot`), and the admin host is
not a route. So today the admin host is served by the **self-signed fallback** (or an operator-mounted cert), not a
real ACME certificate. The user needs a valid ACME cert on the admin host to test on a real Docker host —
*"à minima hôte admin dédié afin de pouvoir avoir un certificat ACME."*

## DockYarp today
- `src/DockYarp.Tls/TlsDomains.cs` — `Desired(RouteConfigSnapshot)` returns the ACME-desired hosts, sourced **only**
  from route TLS metadata. Admin host absent.
- `src/DockYarp.AdminApi/AdminApiOptions.cs` — `Host` (added by `isolate-admin-api`). The admin API also carries a
  contact email conceptually, but ACME contact today comes from route TLS / `Tls:ContactEmail`.
- **Constraint**: `Core` is the leaf; `Tls` must **not** reference `AdminApi` (no cycle, and `Tls` stays a library).
  So the admin host cannot be injected by making `TlsDomains` read `AdminApiOptions`.

## Proposed change (sketch)
- Introduce a small **reserved-hosts cert-desire** seam so non-route hosts (starting with the admin host) can
  request an ACME certificate without `Tls` depending on `AdminApi`:
  - Option A: an `IReservedCertificateHosts` (or a `IReadOnlyList<DesiredCertificate>` DI contribution) that
    `App` populates from `AdminApi:Host` (+ ACME opt-in flags), merged into the desired set alongside
    `TlsDomains.Desired(...)` where provisioning is driven.
  - Gate on the same ACME preconditions as routes (accepted ToS, contact email, reachable :80) — reuse
    `LETSENCRYPT_HOST`-style opt-in semantics for the admin host (e.g. `AdminApi:LetsEncrypt` + contact).
- Ensure SNI selection serves the admin-host cert on the admin host, and HTTP-01 challenge still works on the edge.

## Acceptance criteria (→ scenarios)
- **WHEN** `AdminApi:Host` is set with ACME enabled + accepted ToS + contact **THEN** DockYarp provisions and renews
  an ACME certificate for the admin host, and serves it over HTTPS on that host.
- **WHEN** ACME is not opted in for the admin host **THEN** the admin host keeps the fallback/operator cert
  (unchanged from the `isolate-admin-api` MVP).
- **WHEN** no `AdminApi:Host` is set **THEN** nothing changes (no reserved host, no extra desire).

## Notes / risks / references
- Split-off from **isolate-admin-api** (archived 2026-08-13) — that change is the MVP; this closes the ACME half.
- The user also flagged a **dedicated admin port**: exposing the admin host on a port other than 80/443 would break
  HTTP-01 ACME. Keep the dedicated-port option as a separate follow-up; this item stays on the standard edge ports so
  ACME works.
- Keep the dependency graph clean: the desire seam lives in `Core`/`Tls`; `App` wires `AdminApi:Host` into it.
