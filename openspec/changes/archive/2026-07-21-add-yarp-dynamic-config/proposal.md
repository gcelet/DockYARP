## Why

The `proxy-routing` model and `docker-discovery` fill an in-memory snapshot, but nothing yet serves
traffic. YARP must be driven from that snapshot and reloaded live as it changes. This change bridges the
internal model to YARP and turns DockYarp into an actual reverse proxy.

## What Changes

- Add a **change notification** to the `proxy-routing` store so consumers learn when a new snapshot is
  published (raised only when content actually changes).
- Map the internal model to YARP configuration (`RouteConfig`/`ClusterConfig`), preserving host/path
  matching (including wildcard hosts) and cluster membership.
- Drive YARP's **`InMemoryConfigProvider`** from the store: a bridge updates YARP whenever the store
  raises a change, reloading routes/clusters **without a process restart**.
- Support **load-balancing policies** per cluster (round-robin, least-requests).
- Support **backend health checks** (YARP active and passive) per cluster when configured.
- Wire the ASP.NET host: register the store, add the reverse proxy, and map the proxy pipeline.

## Capabilities

### New Capabilities
- `yarp-dynamic-config`: live `IProxyConfigProvider` driven by the store, model→YARP mapping, per-cluster
  load balancing, and backend health checks.

### Modified Capabilities
- `proxy-routing`: add a store change-notification requirement (needed so YARP can reload on change).

## Impact

- **Code**: `src/DockYarp.App` (YARP wiring, mapping, provider bridge, `Program`); a change event added
  to `DockYarp.Core` `IRouteConfigStore`/`RouteConfigStore`.
- **Dependencies**: add `Yarp.ReverseProxy` to `DockYarp.App` (version already in CPM).
- **Tests**: mapper + bridge unit tests and a boot integration test in `DockYarp.IntegrationTests`.
- **Deferred**: wiring Docker discovery into the host (kept out so this change is testable without a
  Docker daemon); the `AddDockerDiscovery` extension already exists for a later bootstrap change.
- **Owning agent**: AG-RP.
