# Security middleware (DockYarp.Security)

DockYarp runs a small security pipeline in front of the reverse proxy. Per-request decisions are driven
by the matched route from the `proxy-routing` store (see [routing-model.md](routing-model.md)).

## Pipeline order

`UseDockYarpSecurity()` adds, in order:

1. **`SecurityHeadersMiddleware`** — applies baseline headers to every response
   (`X-Content-Type-Options: nosniff`, `X-Frame-Options`, `Referrer-Policy`) and, on HTTPS responses,
   `Strict-Transport-Security` (HSTS). Configurable via `SecurityHeadersOptions`.
2. **`HttpsRedirectionMiddleware`** — if the request is HTTP and its matched route sets
   `HostTlsMetadata.EnforceHttps`, redirects to the HTTPS URL for the same host/path (308).
3. **`BasicAuthMiddleware`** — if the matched route carries `BasicAuthCredentials`, requires a valid
   `Authorization: Basic` header; otherwise responds 401 with `WWW-Authenticate: Basic`. Credentials are
   compared in fixed time and never logged.

It runs **before** `MapReverseProxy()`.

## Route lookup

`RouteLookup` finds the route for a request `(host, path)` by caching a `RouteMatcher` and rebuilding it
only when the store's snapshot `Version` changes, so per-request checks stay cheap.

## Configuration source

- **HTTPS enforcement** reads `HostTlsMetadata.EnforceHttps` (populated from `LETSENCRYPT_*` labels).
  Real certificate-availability checks arrive with `tls-acme`.
- **Basic Auth** reads `RouteRule.Auth`. Parsing `DOCKYARP_AUTH_*` labels into that field is a deferred
  `docker-discovery` update; until then it comes from static configuration or tests.

## Wiring

```csharp
builder.Services.AddDockYarpSecurity(new SecurityHeadersOptions());
...
app.UseDockYarpSecurity();
app.MapReverseProxy();
```
