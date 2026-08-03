# Design — add-external-port-config

## Scope
Only `EXTERNAL_HTTPS_PORT` has a concrete DockYarp target: the port in the HTTP→HTTPS redirect `Location`.
`EXTERNAL_HTTP_PORT` is nginx's HTTP listen port; DockYarp's listeners are global (`Server:HttpPort`/`HttpsPort`),
so there is no per-vhost HTTP port to honor — out of scope (documented).

## Data path (mirrors HSTS / SSL_POLICY — carried in HostTlsMetadata)
```
DockerLabels.ExternalHttpsPort ("EXTERNAL_HTTPS_PORT")
  → LabelParser: ExternalHttpsPort = ParseExternalPort(config)   (int in 1..65535, env wins via EffectiveConfig)
  → ContainerLabelConfig.ExternalHttpsPort
  → ContainerMapper: HostTlsMetadata { ExternalHttpsPort = ... }  (both classic + multiports)
  → HttpsRedirectionMiddleware: redirect authority
```
The redirect only fires for hosts that have `HostTlsMetadata` (they declare `LETSENCRYPT_HOST`/`CERT_NAME` and
redirect), so the TLS metadata is the correct carrier — consistent with `HSTS`/`HTTPS_METHOD`/`SSL_POLICY`.

## Redirect construction
```
authority = tls.ExternalHttpsPort is { } p && p != 443 ? $"{host}:{p}" : host
target    = $"https://{authority}{PathBase}{Path}{QueryString}"
```
- Omit the port when it is 443 (default) so the common case is unchanged.
- The incoming request port is dropped; the external HTTPS port is where clients reach HTTPS regardless of the
  HTTP port the request arrived on.

## Parsing + diagnostic
- `ParseExternalPort`: `int` in `1..65535`, else `null` (ignored).
- `HasInvalidExternalHttpsPort`: present but not a valid port → a warning in `AddCommonWarnings` (matches the
  existing invalid-`DOCKYARP_PROXY_TIMEOUT`/`DOCKYARP_MAX_BODY_SIZE` diagnostics). An ignored/invalid value
  leaves the default port.
