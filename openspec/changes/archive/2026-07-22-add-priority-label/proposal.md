## Why

nginx-proxy lets a container set a routing priority so a chosen vhost wins when several routes could match.
DockYarp already models `RouteRule.Priority` (and the Core `RouteMatcher` honors it), but discovery
never sets it — every discovered route has priority `0` — and the priority is not carried into YARP, so it
does not actually influence request routing.

## What Changes

- Parse a `DOCKYARP_PRIORITY` label into the route's priority (default `0`; a non-numeric value falls
  back to `0` with a warning).
- Map the route priority to YARP's route `Order` (higher priority ⇒ lower order ⇒ higher precedence) so
  priority governs which route serves a request, not just the security matcher.

## Capabilities

### Modified Capabilities
- `docker-discovery`: `DOCKYARP_PRIORITY` sets the route priority.
- `yarp-dynamic-config`: route priority maps to YARP route order (higher priority wins).

## Impact

- **Code**: `src/DockYarp.Docker` (parse the label into `ContainerLabelConfig.Priority` → `RouteRule`),
  `src/DockYarp.App/ReverseProxy` (map priority to `RouteConfig.Order`).
- **Owning agent**: AG-DD / AG-RP.
