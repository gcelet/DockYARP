# Security middleware (DockYarp.Security)

DockYarp runs a small security pipeline in front of the reverse proxy. Per-request decisions are driven
by the matched route from the `proxy-routing` store (see [routing-model.md](routing-model.md)).

## Pipeline order

`UseDockYarpSecurity()` adds, in order:

1. **`SecurityHeadersMiddleware`** — applies baseline headers to every response
   (`X-Content-Type-Options: nosniff`, `X-Frame-Options`, `Referrer-Policy`) and, on HTTPS responses,
   `Strict-Transport-Security` (HSTS). Configurable via `SecurityHeadersOptions` (including `HstsPreload`).
   A matched route's per-host `HSTS` override replaces the header (or `off` suppresses it for that host).
2. **`HttpsRedirectionMiddleware`** — applies the route's HTTPS method. On HTTP: if the method is redirecting
   (`redirect`/`nohttp`) **and** a certificate is available for the host, redirects to the HTTPS URL for the
   same host/path (308); `noredirect`/`nohttps` and a certless host are served over HTTP. On HTTPS: a
   `nohttps` host is refused (404), since it is served over HTTP only.
3. **`ClientCertificateMiddleware`** — if the matched route requires a client certificate
   (`RouteRule.ClientCertificate == Required`) and none was presented on the connection, responds 403.
   Certificates are validated against the configured CA at the TLS handshake; this enforces presence per host.
4. **`BasicAuthMiddleware`** — if the matched route carries `BasicAuthCredentials`, requires a valid
   `Authorization: Basic` header; otherwise responds 401 with `WWW-Authenticate: Basic`. Credentials are
   compared in fixed time and never logged.

It runs **before** `MapReverseProxy()`.

## Route lookup

`RouteLookup` finds the route for a request `(host, path)` by caching a `RouteMatcher` and rebuilding it
only when the store's snapshot `Version` changes, so per-request checks stay cheap.

## Configuration source

- **HTTPS enforcement** reads `HostTlsMetadata.Method` (from `HTTPS_METHOD`, defaulting to `redirect`) and
  gates redirection on `ICertificateAvailability` (an App adapter over the certificate store), so a host is
  only redirected once a certificate exists.
- **Basic Auth** reads `RouteRule.Auth`. Parsing `DOCKYARP_AUTH_*` labels into that field is a deferred
  `docker-discovery` update; until then it comes from static configuration or tests.

## Wiring

```csharp
builder.Services.AddDockYarpSecurity(new SecurityHeadersOptions());
...
app.UseDockYarpSecurity();
app.MapReverseProxy();
```
