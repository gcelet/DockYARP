# Design — isolate-admin-api (MVP: dedicated admin host)

## The mechanism: `RequireHost`
ASP.NET endpoint routing already supports host constraints. Applying `RequireHost(adminHost)` to the admin
endpoints makes them match **only** when the request `Host` equals `adminHost`; on any other host they do not match,
so routing continues to YARP's catch-all (`MapReverseProxy`) → the request is proxied. No precedence hack, no new
middleware.

- **`AdminApiOptions.Host`** (`string?`, default `null`) bound from the `AdminApi` section.
- **`MapAdminApi(this IEndpointRouteBuilder, string? host = null)`**: when `host` is non-empty,
  `group.RequireHost(host)` on the `/api` route group (covers every admin endpoint at once).
- **`Program.cs`**: resolve `AdminApiOptions`, pass `Host` to `MapAdminApi`, and apply `RequireHost` to the
  `/metrics` endpoint too (`MapPrometheusScrapingEndpoint().RequireHost(host)`), so `/metrics` is scoped as well.

## Behavior
| `AdminApi:Host` | Request | Result |
|---|---|---|
| unset | any host `/api/health` | admin endpoint (401 without key) — **unchanged, all hosts** |
| `admin.local` | `admin.local/api/health` | admin endpoint (401 without key) |
| `admin.local` | `app.local/api/health` | **proxied** to the backend (or the default response) — not shadowed |

Unset = backward compatible (existing setups/tests keep working). Setting the host is the opt-in isolation.

## Tests
`AdminApiIntegrationTests` (Mvc.Testing): with `AdminApi:Host=admin.local`, a `/api/health` request with `Host:
other.local` is **not** the admin `401` (falls through), while `Host: admin.local` **is** the admin `401`
(no key). Existing tests (no `AdminApi:Host`) prove the all-hosts default is unchanged.

## Out of scope (→ follow-up `add-admin-host-cert`)
- Provisioning an **ACME certificate** for the admin host. The admin host is served by DockYarp itself (not a
  discovered backend), so it is not in `TlsDomains.Desired(snapshot)`. Adding it needs a small **reserved-hosts**
  cert-desire source injected from the App layer (TLS must not reference `DockYarp.AdminApi`). Until then the admin
  host uses the self-signed fallback (or an operator-provided cert).
- Dedicated admin **port** (with its own ACME story on non-80/443 ports).
