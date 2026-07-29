## Why
nginx-proxy can restrict a vhost to internal networks with `NETWORK_ACCESS=internal`, returning 403 to clients
outside a configurable set of private ranges — common for admin/staging hosts. DockYarp has no per-host
client-IP access control.

## What Changes
- **Label**: `NETWORK_ACCESS=internal` marks a route internal-only (per host and per multiports path).
- **Model**: `RouteRule.InternalOnly` (bool).
- **Config**: a configurable `Security:InternalRanges` list of CIDR networks (default the private ranges
  `127.0.0.0/8`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `::1/128`).
- **Middleware**: a `NetworkAccessMiddleware` that, for an internal-only route, returns **403** unless the
  client IP is within an internal range. It resolves the client IP from `HttpContext.Connection.RemoteIpAddress`
  (fail-closed: an unknown IP is treated as external) and normalizes IPv4-mapped IPv6 addresses.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: a route may be restricted to internal client networks, returning 403 to external clients.

## Impact
- **Code**: `DockYarp.Core` (`RouteRule`), `DockYarp.Docker` (`DockerLabels`, `LabelParser`,
  `ContainerLabelConfig`, `ContainerMapper`), `DockYarp.Security` (`SecurityHeadersOptions`,
  `NetworkAccessMiddleware`, DI + pipeline registration).
- **Tests**: `DockYarp.Security.Tests` (internal allowed / external 403 / custom ranges / unknown IP),
  `DockYarp.Docker.Tests` (`NETWORK_ACCESS=internal` → `InternalOnly`).
- **Client-IP caveat**: enforcement keys on the direct connection IP (`RemoteIpAddress`), which is correct when
  DockYarp is the edge (matching nginx's default `$remote_addr`). Behind a trusted L7 proxy, resolving the real
  client requires inbound forwarded-header/PROXY-protocol trust — DockYarp does not yet rewrite the inbound
  `RemoteIpAddress`, so that is deferred (tracked alongside `add-proxy-protocol`). The middleware reads
  `RemoteIpAddress`, so it upgrades automatically once that trust layer lands.
- **Owning agent**: AG-SEC. Resolves `add-network-access-internal`.
