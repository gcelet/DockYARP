# Design — add-network-access-internal

## Enforcement
A new `NetworkAccessMiddleware` runs in the security pipeline **after** the security headers and **before**
HTTPS redirection, so an external client hitting an internal-only host is denied (403) outright rather than
redirected or served. It resolves the request's route via the existing `RouteLookup` (cached per request) and,
when `route.InternalOnly` is set, checks the client IP against the configured internal ranges.

Pipeline order becomes: headers → **network-access** → HTTPS redirect → client certificate → Basic Auth.

## Client IP
The client IP is `HttpContext.Connection.RemoteIpAddress`:
- **Fail closed**: a null address (no connection info) is treated as external → 403.
- **IPv4-mapped IPv6** (`::ffff:10.0.0.1`, common with Kestrel dual-stack sockets) is normalized with
  `MapToIPv4()` before range checks so it matches IPv4 CIDRs.

DockYarp does not currently process inbound `X-Forwarded-For` (no `UseForwardedHeaders`), so `RemoteIpAddress`
is the direct TCP peer — correct when DockYarp is the edge, and spoofing-proof (unlike trusting a client-sent
`X-Forwarded-For`). Behind a trusted proxy, real-client resolution needs an inbound trust layer; reading
`RemoteIpAddress` means the middleware picks that up automatically once it exists.

## Ranges
`Security:InternalRanges` is a list of CIDR strings, defaulting to `127.0.0.0/8`, `10.0.0.0/8`,
`172.16.0.0/12`, `192.168.0.0/16`, `::1/128`. They are parsed once (via `System.Net.IPNetwork.TryParse`) into an
`IPNetwork[]` when the middleware is constructed; an invalid entry is skipped (not fatal). Matching uses
`IPNetwork.Contains`.

## Model + labels
`RouteRule.InternalOnly` is set from `NETWORK_ACCESS=internal` (case-insensitive; any other value is treated as
not-internal). It is parsed for both the classic route (`LabelParser.TryParse`) and the shared multiports
attributes (`LabelParser.ParseCommon`), and threaded through `ContainerMapper` on both route-building paths.

## Not in scope
Deny/allow lists beyond "internal", per-host custom ranges, and trusted-proxy real-client resolution. The last
is noted against `add-proxy-protocol`.
