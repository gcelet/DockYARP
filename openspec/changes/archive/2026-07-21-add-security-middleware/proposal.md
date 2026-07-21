## Why

Once a host serves traffic, HTTP requests should be upgraded to HTTPS, sensitive routes protected, and
every proxied response hardened with security headers — matching nginx-proxy behavior. This adds the
security middleware pipeline in front of the reverse proxy.

## What Changes

- Add **HTTP→HTTPS redirect** middleware, applied per host when the host's route requests HTTPS
  enforcement (`HostTlsMetadata.EnforceHttps`).
- Add **Basic Auth** middleware that protects a route when it carries auth credentials, returning 401 with
  a `WWW-Authenticate` challenge otherwise. Credentials are read from the routing model.
- Add **security headers** (HSTS on HTTPS, plus `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`), configurable.
- Add optional **auth metadata** to the routing model so a route can carry Basic Auth credentials.
- Wire the pipeline in the host: security runs before the reverse proxy.

## Capabilities

### New Capabilities
- `security`: per-host HTTPS enforcement, route Basic Auth, and configurable security headers.

### Modified Capabilities
- `proxy-routing`: add optional per-route authentication metadata (Basic Auth credentials).

## Impact

- **Code**: `src/DockYarp.Security` (middlewares + DI/app extensions); an auth field added to
  `DockYarp.Core` `RouteRule`; pipeline wiring in `DockYarp.App`.
- **Dependencies**: `DockYarp.Security` references the ASP.NET shared framework
  (`Microsoft.AspNetCore.App`).
- **Deferred**: parsing `DOCKYARP_AUTH_*` labels in `docker-discovery` (a later change) — for now the
  auth metadata is populated by static configuration or seeded in tests.
- **Owning agent**: AG-SEC.
