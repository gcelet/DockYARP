## Why

nginx-proxy's `VIRTUAL_DEST` rewrites the request path before forwarding (e.g. strip the `VIRTUAL_PATH`
prefix). DockYarp models a `RouteTransforms.PathRemovePrefix` placeholder but never applies it, and
discovery never derives it — so path-based routes always forward the full original path.

## What Changes

- Apply the route's path transform in the YARP mapping: when `PathRemovePrefix` is set, strip it from the
  forwarded path (wiring the existing placeholder into a YARP transform).
- Derive the transform from labels: a `VIRTUAL_DEST` (or the convention of stripping `VIRTUAL_PATH`) sets
  `PathRemovePrefix` so the backend receives the rewritten path.

## Capabilities

### Modified Capabilities
- `yarp-dynamic-config`: route path transforms are applied to forwarded requests.
- `docker-discovery`: `VIRTUAL_DEST` configures the path rewrite.

## Impact

- **Code**: `src/DockYarp.App/ReverseProxy` (YARP transform), `src/DockYarp.Docker` (derive transform
  from `VIRTUAL_DEST`/`VIRTUAL_PATH`).
- **Owning agent**: AG-RP / AG-DD.
