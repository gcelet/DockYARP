## 1. Store change notification (AG-RP)

- [x] 1.1 Add `event EventHandler? Changed` to `IRouteConfigStore`
- [x] 1.2 Raise `Changed` in `RouteConfigStore.Apply` only when content changed (reuse no-op detection)
- [x] 1.3 Unit tests: notified on content change, not notified on no-op

## 2. Model to YARP mapping (AG-RP)

- [x] 2.1 Add `Yarp.ReverseProxy` PackageReference to `DockYarp.App`
- [x] 2.2 Implement `YarpConfigMapper` (routes → `RouteConfig` with host + optional path prefix; wildcard hosts)
- [x] 2.3 Map clusters → `ClusterConfig` (destinations keyed by endpoint id)
- [x] 2.4 Map `LoadBalancingPolicy` to YARP policy constants (round-robin, least-requests)
- [x] 2.5 Map `HealthCheckConfig` to YARP active/passive health checks when present
- [x] 2.6 Unit tests for the mapper (host/path, LB policy, health check)

## 3. Provider bridge & host wiring (AG-RP)

- [x] 3.1 Add `LoadFromMemory([], [])` and resolve `InMemoryConfigProvider`
- [x] 3.2 Implement `YarpConfigBridge` hosted service: push current snapshot on start, subscribe to `Changed`, `Update` on change, unsubscribe on stop
- [x] 3.3 Wire `Program`: register `IRouteConfigStore` singleton, `AddReverseProxy`, the bridge, `MapReverseProxy`
- [x] 3.4 Make `Program` testable (`public partial class Program`)

## 4. Tests (AG-RP)

- [x] 4.1 Unit test the bridge: a store change updates the YARP `InMemoryConfigProvider` config
- [x] 4.2 Integration test: the app boots and a request to an unmatched host returns 404 (YARP wired to the store)
- [x] 4.3 Integration test: after seeding a route to a stub backend, a request is proxied to it

## 5. Documentation (AG-RP)

- [x] 5.1 Document the YARP integration (mapping, live reload, LB, health) in `docs/`
