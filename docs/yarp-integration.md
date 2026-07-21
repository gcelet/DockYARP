# YARP integration (DockYarp.App)

DockYarp serves traffic with [YARP](https://github.com/dotnet/yarp), driven by the `proxy-routing`
store. Only `DockYarp.App` depends on YARP; `DockYarp.Core` stays YARP-free.

## Flow

```
docker-discovery / static config ──> IRouteConfigStore.Apply(...)
                                          │ (content changed)
                                          ▼  Changed event
                                   YarpConfigBridge ──> YarpConfigMapper ──> InMemoryConfigProvider.Update(...)
                                                                                     │
                                                                                     ▼
                                                                             YARP reloads (no restart)
```

- **`IRouteConfigStore.Changed`** (Core) fires when a new snapshot is published (never on a no-op).
- **`YarpConfigBridge`** (hosted service) pushes the current snapshot on start, then on every `Changed`
  maps the snapshot and calls YARP's built-in `InMemoryConfigProvider.Update`, so routes/clusters reload
  live without a process restart.
- **`YarpConfigMapper`** converts the internal model to YARP config.

## Mapping

| Internal | YARP |
|---|---|
| `RouteRule.HostPattern` | `RouteMatch.Hosts` (YARP handles `*.suffix` wildcards natively) |
| `RouteRule.PathPrefix` | `RouteMatch.Path` = `"{prefix}/{**catch-all}"` (null ⇒ match any path) |
| `RouteRule.ClusterId` | `RouteConfig.ClusterId` |
| `Cluster.Endpoints` | `ClusterConfig.Destinations` (keyed by endpoint/container id) |
| `LoadBalancingPolicy` | `ClusterConfig.LoadBalancingPolicy` (`RoundRobin`, `LeastRequests`) |
| `HealthCheckConfig` | `ClusterConfig.HealthCheck` (active + passive) when present |

## Host wiring (`Program`)

```csharp
builder.Services.AddSingleton<IRouteConfigStore, RouteConfigStore>();
builder.Services.AddReverseProxy().LoadFromMemory([], []); // empty until the store publishes
builder.Services.AddHostedService<YarpConfigBridge>();
...
app.MapReverseProxy();
```

Docker discovery is intentionally **not** wired here — it is added in a later bootstrap/deployment change
(the `AddDockerDiscovery` extension already exists). Until then the store can be fed by tests or static
configuration.
