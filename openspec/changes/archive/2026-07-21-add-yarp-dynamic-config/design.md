## Context

`proxy-routing` holds an immutable, versioned snapshot; `docker-discovery` publishes into it. YARP has its
own configuration model (`RouteConfig`/`ClusterConfig`) served through `IProxyConfigProvider`. Only
`DockYarp.App` may depend on YARP (Core stays YARP-free). We need YARP to reflect the store live, with
no restart, driven by an explicit change signal rather than polling.

## Goals / Non-Goals

**Goals:**
- A store change notification (Core) raised only on real content changes.
- A faithful model→YARP mapping (host + optional path, wildcard hosts, cluster destinations, LB, health).
- Live reload of YARP on every store change.
- Wire the host so DockYarp actually proxies.

**Non-Goals:**
- No Docker discovery wiring in the host (deferred; keeps this testable without a daemon).
- No request transforms beyond what the model already carries (future).
- No TLS/HTTPS (that is `tls-acme` / `security`).

## Decisions

- **Change notification via a plain .NET event** on `IRouteConfigStore` (`event EventHandler? Changed`),
  raised inside `Apply` only when content changed (reusing the existing no-op detection). Rationale: no new
  Core dependency (rejected `IChangeToken`/`Microsoft.Extensions.Primitives` to keep Core a BCL-only leaf).
  This is an additive requirement on `proxy-routing` (ADDED, not MODIFIED — existing behavior is unchanged).
- **YARP `InMemoryConfigProvider`** (built-in) over a hand-written `IProxyConfigProvider`. Rationale: it
  already implements atomic reload via `Update(routes, clusters)`; we only supply a mapping and a bridge.
- **Bridge as a hosted service** (`YarpConfigBridge`) that, on start, pushes the current snapshot and
  subscribes to `Changed`; each change maps the snapshot and calls `InMemoryConfigProvider.Update`.
  Rationale: keeps YARP dumb and the store authoritative; unsubscribes on stop.
- **Mapping** (`YarpConfigMapper`, pure/static in App): route → `RouteConfig` with `Match.Hosts=[host]`
  (YARP handles `*.suffix` wildcards natively) and `Match.Path = "{prefix}/{**catch-all}"` when a prefix is
  set; cluster → `ClusterConfig` with destinations keyed by endpoint id, `LoadBalancingPolicy` mapped to
  YARP's policy constants, and health checks mapped from `HealthCheckConfig` when present.
- **Host wiring**: register `RouteConfigStore` as the singleton `IRouteConfigStore`, `AddReverseProxy()` +
  `LoadFromMemory([], [])` (empty initial), the bridge hosted service, and `MapReverseProxy()`.

## Risks / Trade-offs

- Event handler runs on the caller's thread (the discovery/reconcile thread). Mapping + `Update` are cheap
  and infrequent → acceptable. [Risk: a slow handler blocks the publisher] → mapping is allocation-light and
  synchronous; no I/O.
- `InMemoryConfigProvider.Update` replaces the whole config each time → fine given low update frequency and
  the store's no-op suppression upstream.

## Migration Plan

Additive. Adds a `Yarp.ReverseProxy` reference to `DockYarp.App` and an event member to the store.
`Program` becomes testable (`public partial class Program`).

## Open Questions

- Passive health-check thresholds and active-probe policy names — start with YARP defaults, revisit when a
  health requirement drives specifics.
