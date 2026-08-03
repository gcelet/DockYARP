---
id: add-external-port-config
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: backlog
nginx-proxy: EXTERNAL_HTTP_PORT / EXTERNAL_HTTPS_PORT (per-vhost)
provenance: 2026-07-31 env-var compat pass (Group-A gap, no prior item)
---

## Why
nginx-proxy's per-vhost `EXTERNAL_HTTP_PORT`/`EXTERNAL_HTTPS_PORT` set the external ports a vhost is reached on,
which it uses when generating HTTP→HTTPS redirect URLs. DockYarp's HTTP→HTTPS redirect always targets the host
with no explicit port (→ 443), so behind a non-standard published HTTPS port the redirect `Location` is wrong.

## nginx-proxy behavior
- `EXTERNAL_HTTPS_PORT` (default = global `HTTPS_PORT`/443): the external HTTPS port for the vhost, used in the
  redirect target. `EXTERNAL_HTTP_PORT` is the external HTTP listen port.

## DockYarp today
- `HttpsRedirectionMiddleware` builds `https://{host}{path}` — no port, i.e. always the default 443.
- DockYarp's HTTP/HTTPS listeners are global (`Server:HttpPort`/`HttpsPort`); there is no per-vhost HTTP port.

## Proposed change (sketch)
- Recognize per-container `EXTERNAL_HTTPS_PORT`, carry it into the route's TLS metadata, and use it as the port
  in the HTTP→HTTPS redirect `Location` (omit the port when it is 443).
- `EXTERNAL_HTTP_PORT` has no per-vhost target in DockYarp (the HTTP listener is global) → out of scope, noted.

## Acceptance criteria (→ scenarios)
- **WHEN** a redirecting host declares `EXTERNAL_HTTPS_PORT=8443` **THEN** its HTTP→HTTPS redirect targets
  `https://<host>:8443/…`.
- **WHEN** a redirecting host declares no `EXTERNAL_HTTPS_PORT` **THEN** the redirect targets `https://<host>/…`
  (default port, unchanged).

## Notes / risks / references
- Small: parsing + one `HostTlsMetadata` field + the redirect authority in the existing middleware.
  Unit-testable, no e2e. Only `EXTERNAL_HTTPS_PORT` has a concrete effect (the redirect port).
