## Why
nginx-proxy's `ENABLE_HTTP_ON_MISSING_CERT` and `TRUST_DEFAULT_CERT` are per-vhost overridable. DockYarp
implements both only **globally** (`Security:EnableHttpOnMissingCert`, `Security:TrustDefaultCert`), so a single
host cannot opt out. This adds the per-host overrides, completing the per-vhost TLS-enforcement family.

## What Changes
- Recognize per-container `ENABLE_HTTP_ON_MISSING_CERT` and `TRUST_DEFAULT_CERT` (env var or label, env wins;
  the namespaced `com.github.nginx-proxy.nginx-proxy.trust-default-cert` is accepted as an alias for the latter),
  parsed as booleans and carried into the route's TLS metadata.
- In `HttpsRedirectionMiddleware`, a route's value takes precedence over the global default: a host with
  `TRUST_DEFAULT_CERT=false` refuses HTTPS (500) when it has no real certificate even if the global default is
  `true`; a host with `ENABLE_HTTP_ON_MISSING_CERT=false` is redirected even without a certificate.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: the HTTP-on-missing-cert and trust-default-cert policies can be overridden per host (the route's
  value wins over the global default).

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels` (two keys + the namespaced trust-default-cert alias),
  `ContainerLabelConfig` (two `bool?`), `LabelParser` (a `ParseBool` helper; both in `TryParse` + `ParseCommon`),
  `ContainerMapper` carries them into `HostTlsMetadata`. `DockYarp.Core` — `HostTlsMetadata` (two `bool?`).
  `DockYarp.Security` — `HttpsRedirectionMiddleware` uses `tls.X ?? options.X`.
- **Tests (unit)**: `LabelParser` parse (incl. the namespaced trust-default-cert alias); `ContainerMapper`
  carries them; `HttpsRedirectionMiddleware` per-host `TrustDefaultCert=false` (500) and
  `EnableHttpOnMissingCert=false` (forced redirect) overriding a permissive global default.
- **Runtime / e2e**: none (redirect/refusal behavior is fully unit-testable).
- **Owning agent**: AG-SEC (with AG-DD for parsing). Resolves `add-missing-cert-behavior-per-vhost`.
