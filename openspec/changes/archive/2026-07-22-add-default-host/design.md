## Context

DockYarp returns a bare 404 for any host that matches no route. nginx-proxy instead lets an operator
nominate a `DEFAULT_HOST` (a backend that also serves unknown hosts) and a configurable default response
for genuinely unmatched requests. Routing happens in two matchers: YARP's own `RouteConfig` matching (real
request proxying) and Core's `RouteMatcher` (used by the security middleware to pick auth/TLS per host).
Both must learn the default host to stay consistent, and the App needs a terminal fallback for the default
response.

## Goals / Non-Goals

**Goals:** a configurable default host whose route also matches unknown hosts (Core matcher fallback + YARP
catch-all route so the backend is actually reached and security still applies); a configurable default
status code for unmatched requests (default 404).

**Non-Goals:** redirect/custom-body default responses beyond a status code, per-host default pages
(`vhost.d`), `DEFAULT_ROOT` file serving — deferred.

## Decisions

- **`RoutingOptions` in `DockYarp.Core.Configuration`**: `DefaultHost` (string?, null disables) and
  `DefaultResponseStatusCode` (int, default 404). Bound from a new `Routing` config section by the host.
- **`RouteMatcher` default-host fallback**: optional `defaultHost` ctor argument. After exact + wildcard
  matching fail, the matcher tries the default host's own route bucket (path-matched), returning its route.
  Allocation-free, on the request hot path. Unknown default host (no route yet) ⇒ no fallback.
- **`RouteLookup` threads the option** into the matcher so the security middleware applies the default
  host's auth/TLS to unknown-host requests it will proxy.
- **YARP catch-all route**: `YarpConfigMapper.Map` takes an optional `defaultHost`; when set and a route for
  it exists, it appends one `RouteConfig` with no host match (matches any host), lowest precedence
  (`Order = int.MaxValue`), reusing the default route's cluster and transforms — so unknown hosts are
  proxied to the chosen backend while specific host routes still win.
- **Default response**: the host maps a terminal `MapFallback` returning `DefaultResponseStatusCode`. When a
  default host is configured its catch-all absorbs unknown hosts, so the fallback only fires when there is
  no default host — matching the requirement ("no route and no default host").

## Risks / Trade-offs

- `RouteLookup` and `YarpConfigBridge` gain a `RoutingOptions` dependency; the host must register it (it
  does). Direct test construction of `RouteLookup` passes `new RoutingOptions()`.
- The catch-all reuses the default route's cluster/transforms rather than cloning match semantics; adequate
  for a default backend, and specific routes always take precedence by order.

## Migration Plan

Additive: new options record + optional matcher/mapper arguments + one fallback endpoint. No persisted
state; behavior is unchanged when `Routing` is not configured (default 404, no default host).

## Open Questions

- Redirect / custom-body default responses and per-host default pages — deferred to a later change.
