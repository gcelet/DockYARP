## Why

nginx-proxy's `HTTPS_METHOD` controls per-vhost HTTP↔HTTPS behavior (`redirect`, `noredirect`, `nohttp`,
`nohttps`), and it only redirects to HTTPS once a certificate actually exists. DockYarp redirects purely
on a boolean `EnforceHttps` flag with **no certificate-availability check** — so an HTTP request can be
redirected to an HTTPS endpoint that has no certificate yet (before ACME completes), breaking the site.

## What Changes

- Replace the `HostTlsMetadata.EnforceHttps` flag with an **`HttpsMethod`** (`redirect` default,
  `noredirect`, `nohttp`, `nohttps`) parsed from the `HTTPS_METHOD` label.
- Redirect HTTP→HTTPS only when the method is redirecting (`redirect`/`nohttp`) **and a certificate is
  available** for the host; `noredirect`/`nohttps` never redirect. Certificate availability is checked
  through a new `ICertificateAvailability` abstraction (App adapter over the certificate store).

## Capabilities

### Modified Capabilities
- `docker-discovery`: `HTTPS_METHOD` sets the route's HTTPS method.
- `proxy-routing`: per-host TLS metadata carries an HTTPS method (instead of a bare enforcement flag).
- `security`: HTTP→HTTPS redirection is driven by the method and gated on real certificate availability.

## Impact

- **Code**: `src/DockYarp.Core` (`HttpsMethod`, `HostTlsMetadata`), `src/DockYarp.Docker` (parse label,
  set method), `src/DockYarp.Security` (`ICertificateAvailability`, redirection middleware),
  `src/DockYarp.App` (availability adapter + wiring), `src/DockYarp.AdminApi` (TLS view).
- **Deferred**: not serving a protocol at the listener level (`nohttp`/`nohttps` beyond redirection) and
  skipping ACME provisioning for `nohttps` — to `add-tls-hardening`.
- **Owning agent**: AG-AT / AG-SEC / AG-DD.
