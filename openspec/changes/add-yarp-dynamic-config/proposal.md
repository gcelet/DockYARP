## Why

The `proxy-routing` model and `docker-discovery` fill an in-memory snapshot, but nothing yet serves
traffic. YARP needs to be driven from that snapshot and reloaded live as it changes. This change bridges
the internal model to YARP.

> Status: **sketch** — proposal + spec intent only. Design and tasks to be detailed just-in-time via
> `/opsx:propose` (or `openspec instructions`) when this phase starts.

## What Changes

- Implement a YARP `IProxyConfigProvider` driven by the `proxy-routing` snapshot store, reloading on
  version change without a process restart.
- Map internal clusters/routes to YARP `ClusterConfig`/`RouteConfig`.
- Support load-balancing policies per cluster (at least round-robin and least-requests).
- Add backend health checks (YARP active and passive) configurable per cluster.

## Capabilities

### New Capabilities
- `yarp-dynamic-config`: live `IProxyConfigProvider`, model→YARP mapping, per-cluster load balancing, and
  backend health checks.

### Modified Capabilities
<!-- None: consumes proxy-routing. -->

## Impact

- **Code**: `src/DockYarp.App` (YARP wiring) + mapping types; integration tests in `DockYarp.IntegrationTests`.
- **Dependencies**: add YARP (`Yarp.ReverseProxy`) via `Directory.Packages.props`.
- **Upstream**: requires `add-proxy-routing-model`. **Owning agent**: AG-RP.
