## Why
DockYarp's HTTP→HTTPS redirect always targets the host with no explicit port (→ 443). Behind a non-standard
published HTTPS port, the redirect `Location` sends clients to the wrong port. nginx-proxy solves this with the
per-vhost `EXTERNAL_HTTPS_PORT`.

## What Changes
- Recognize a per-container `EXTERNAL_HTTPS_PORT` (env var or label; environment wins via the existing merge),
  carried into the route's TLS metadata.
- The HTTP→HTTPS redirect targets `https://{host}:{EXTERNAL_HTTPS_PORT}{path}` when the host declares one
  (the port is omitted when it is 443); otherwise the redirect is unchanged (`https://{host}{path}`).
- `EXTERNAL_HTTP_PORT` has no per-vhost target in DockYarp (the HTTP/HTTPS listeners are global
  `Server:HttpPort`/`HttpsPort`) and is out of scope.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: the HTTP→HTTPS redirect uses the route's external HTTPS port (`EXTERNAL_HTTPS_PORT`) when declared.

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels.ExternalHttpsPort`, `ContainerLabelConfig.ExternalHttpsPort`,
  `LabelParser` parse + `HasInvalidExternalHttpsPort` diagnostic, `ContainerMapper` carries it into
  `HostTlsMetadata`. `DockYarp.Core` — `HostTlsMetadata.ExternalHttpsPort`. `DockYarp.Security` —
  `HttpsRedirectionMiddleware` builds the redirect authority with the port.
- **Tests (unit)**: `LabelParser` parse + invalid diagnostic; `ContainerMapper` carries it;
  `HttpsRedirectionMiddleware` redirects to the configured port (and omits it at 443).
- **Runtime / e2e**: none (redirect behavior is fully unit-testable).
- **Owning agent**: AG-SEC (with AG-DD for parsing). Resolves `add-external-port-config`.
