# Routing model (DockYarp.Core)

The routing model is the YARP-independent heart of DockYarp. Docker discovery and static
configuration write into it; the YARP integration reads from it. It lives in `DockYarp.Core` and
depends only on the BCL.

## Types

| Type | Role |
|---|---|
| `ClusterEndpoint` | A backend destination: a stable `Id` (e.g. container id) + an absolute `Address`. |
| `Cluster` | A backend service: `Id`, `Endpoints`, `LoadBalancingPolicy`, optional `HealthCheck`. |
| `LoadBalancingPolicy` | `RoundRobin` or `LeastRequests` (executed by the proxy layer, not here). |
| `RouteRule` | Maps a `HostPattern` (+ optional `PathPrefix`, `Priority`) to a `ClusterId`; carries optional `Tls` and `Transforms`. |
| `HostTlsMetadata` | Per-host TLS intent: `CertificateHost`, `ContactEmail`, `EnforceHttps`. |
| `RouteConfigSnapshot` | Immutable set of routes + clusters + a monotonic `Version`. |

## Store — `IRouteConfigStore` / `RouteConfigStore`

- Publishes an immutable `RouteConfigSnapshot`; reads are **lock-free** (a volatile reference read).
- `Apply(routes, clusters)` swaps the published reference **atomically** under a write lock — a reader
  sees either the whole old snapshot or the whole new one, never a partial update.
- The `Version` increases **only when content changes**. Re-applying identical content is a **no-op**:
  the same snapshot reference and version are kept, so downstream consumers are not churned.
  (`ImmutableArray<T>` compares by underlying-array reference, so content equality is computed
  element-wise in `RouteConfigSnapshot.HasSameContent`.)

## Matching — `RouteMatcher`

Built once from a snapshot's routes; `TryMatch(host, path, out route)` selects a route by, in order:

1. **Exact host** match preferred over a **single-level wildcard** (`*.suffix`).
2. Highest **`Priority`**.
3. Longest matching **path prefix** (`PathPrefix` null/empty matches any path).

Host comparison is `OrdinalIgnoreCase`; path comparison is `Ordinal`. The request path is matched
without allocation. Wildcard matching is single-suffix (`*.local` matches `app.local`); multi-level
patterns are out of scope until a requirement needs them.

```
routes:
  app.local            -> exact
  *.local              -> wild
matcher.TryMatch("app.local", "/") => exact     # exact beats wildcard

  app.local  "/"       -> root
  app.local  "/api"    -> api
matcher.TryMatch("app.local", "/api/orders") => api   # longest prefix wins
```

## Configuration sources & precedence — `RouteConfigMerger`

Routes/clusters can come from several `ConfigContribution`s, each tagged with a `ConfigSource`
(`Static` or `Dynamic`). `Merge` produces a deterministic, consistent set:

- **Precedence**: `Static` wins over `Dynamic` on conflicts (same cluster id, or same host+path route).
- **Validation**: an entry is skipped when its host or cluster id is missing, or when it references an
  undefined cluster.
- **Resilience**: a single invalid entry is skipped, never discarding the rest of a source.
- **No logging in Core**: findings are returned as `MergeDiagnostic`s (codes `cluster.conflict`,
  `route.conflict`, `route.invalid`, `route.cluster-missing`); callers that own an `ILogger` log them.

The merged routes/clusters are then published via `IRouteConfigStore.Apply`.
