---
id: isolate-admin-api
capability: admin-api
agent: AG-AA
tier: A-structural
priority: high
status: backlog
nginx-proxy: (internal — management-interface isolation, cf. Traefik dashboard on a separate entrypoint)
provenance: 2026-08-11 user found the collision reviewing the "Path routing with a rewrite" example — BLOCKING for real use
---

## Why
The Admin API (and `/metrics`) is mounted on the **data-plane port, on ALL hosts, at fixed `/api/*` paths**, with
**precedence over YARP's catch-all** (`src/DockYarp.App/Program.cs:118-121`). So a backend that itself exposes an
admin sub-path — **`/api/health`** (extremely common), `/api/routes`, `/api/clusters`, `/api/certs`, `/api/resolve`,
`/api/version`, or `/metrics` — on **any** host is **shadowed**: the request hits DockYarp's admin endpoint (401
without the API key) instead of being proxied to the backend. **This is a blocking issue** for a real Docker host
running many services whose apps use `/api/*` routes. nginx-proxy/Traefik isolate the management interface.

## Current state
- `app.MapAdminApi()` maps `/api/{version,routes,clusters,certs,resolve,health}` globally (no host/port/prefix
  constraint); `MapPrometheusScrapingEndpoint()` maps `/metrics`; both take precedence over `MapReverseProxy()`.
- `AdminApiOptions` = API key only. No isolation knob.

## Proposed change (sketch)
- **Dedicated admin host (PRIMARY / MVP — unblocks real use):** `AdminApi:Host` — the admin endpoints (`/api/*` +
  `/metrics`) match **only** when the request `Host` equals it (`RequireHost(...)`). On every other host, `/api/*`
  falls through to YARP and is proxied normally. The admin host is served by DockYarp itself (not proxied) and can
  get an **ACME cert like any vhost** on 443 (works with the existing per-SNI handshake). Needs a way to register
  the admin host for provisioning (e.g. `AdminApi:LetsEncryptHost`/reuse the host) or an operator-provided cert.
- **Configurable path prefix (optional):** mount admin under a configurable prefix (keep `/api`, or default to
  something unlikely like `/_dockyarp`) — a lighter mitigation that complements host isolation.
- **Dedicated port (optional, later):** a separate Kestrel endpoint for admin. ⚠️ **ACME caveat:** a port other than
  80/443 breaks HTTP-01 / TLS-ALPN-01 challenges, so exposing it *with a cert* needs a story — reuse the SNI cert on
  the admin port via the same handshake callback, or serve the admin port as internal plain-HTTP behind the host
  firewall, or an operator-provided cert. Decide in design.

## Acceptance criteria (→ scenarios)
- **WHEN** `AdminApi:Host` is set and a request for a **different** host hits `/api/health` (or another admin
  sub-path) **THEN** it is proxied to the matching backend, not shadowed by the admin endpoint.
- **WHEN** a request for the configured admin host hits `/api/*` **THEN** the admin endpoint serves it (behind the
  API key), and the admin host is served over HTTPS with a valid certificate.
- **WHEN** `AdminApi:Host` is unset **THEN** behavior is a safe, documented default (decide: keep current all-hosts
  behavior for back-compat, or require the host).

## Notes / risks / references
- Structural + unit/integration-testable (`RequireHost` + a Mvc.Testing check that `other.local/api/health` proxies
  while `admin.local/api/health` serves the admin). The ACME-for-admin-host + dedicated-port parts are runtime.
- Cover **`/metrics`** too (same shadowing). Refs: `src/DockYarp.App/Program.cs:118-121`,
  `AdminEndpoints.MapAdminApi`, `AdminApiOptions`. Consider a doc caveat in `examples.md` (the "Path routing with a
  rewrite" example) until this lands.
