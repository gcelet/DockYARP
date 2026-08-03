# Design — add-missing-cert-behavior-per-vhost

## Data path (mirrors HSTS / SSL_POLICY — carried in HostTlsMetadata)
```
DockerLabels.EnableHttpOnMissingCert ("ENABLE_HTTP_ON_MISSING_CERT")
DockerLabels.TrustDefaultCert ("TRUST_DEFAULT_CERT")  + NginxTrustDefaultCert (namespaced label, alias)
  → LabelParser: ParseBool(...)   (env wins via EffectiveConfig; plain key wins over the namespaced alias)
  → ContainerLabelConfig.{EnableHttpOnMissingCert, TrustDefaultCert}  (bool?)
  → ContainerMapper: HostTlsMetadata { EnableHttpOnMissingCert, TrustDefaultCert }  (both classic + multiports)
  → HttpsRedirectionMiddleware
```
Both checks already run only for hosts that have `HostTlsMetadata` (the middleware returns early otherwise), so
the TLS metadata is the correct carrier — consistent with `HSTS`/`HTTPS_METHOD`/`SSL_POLICY`.

## Precedence (per-host over global)
```
trustDefaultCert       = tls.TrustDefaultCert       ?? options.TrustDefaultCert
enableHttpOnMissingCert = tls.EnableHttpOnMissingCert ?? options.EnableHttpOnMissingCert
```
- `TRUST_DEFAULT_CERT=false` on a host → refuse HTTPS with 500 when it has no real certificate, even if the
  global default trusts the default cert.
- `ENABLE_HTTP_ON_MISSING_CERT=false` on a host → force the HTTP→HTTPS redirect even without a certificate.

## Value parsing
`ParseBool`: `true`/`on`/`yes`/`1`→true, `false`/`off`/`no`/`0`→false (case-insensitive), otherwise `null`
(unset → the global default applies). No new diagnostic (an unrecognized value simply falls back, matching how
other optional knobs behave).

## Alias
`TRUST_DEFAULT_CERT` is read from the plain key first, then the namespaced
`com.github.nginx-proxy.nginx-proxy.trust-default-cert` (the per-vhost label nginx-proxy uses) — this is the
namespaced label deferred from `add-nginx-label-aliases`. `ENABLE_HTTP_ON_MISSING_CERT` uses the plain key only
(nginx-proxy's per-vhost channel for it is the env var of the same name).
