## Why
DockYarp reconciles on **every** Docker lifecycle event: `DockerDiscoveryService` runs a full
`ReconcileAsync` per `start`/`stop`/`die`/`update`. During a rollout, where many containers change within
a fraction of a second, this fires one reconcile (list + map + merge + store publish + YARP reload) per
event — redundant work and store/YARP churn. nginx-proxy's docker-gen avoids this with `-wait min:max`,
which coalesces a burst of events into a single regeneration.

## What Changes
- Debounce the **event-driven** reconcile: after an event, wait a short quiet window
  (`Docker:ReconcileDebounceMin`, extended by each further event) and reconcile once for the whole burst,
  never deferring longer than a hard cap (`Docker:ReconcileDebounceMax`) from the first event of the burst.
- A single (sparse) event still reconciles promptly, within the quiet window.
- **Startup** and **post-reconnect** reconciliations stay immediate — they are not event-driven and are
  unaffected by debouncing.
- The window is configurable with sensible defaults (250 ms / 2 s); `ReconcileDebounceMin = 0` disables
  debouncing (reconcile per event, the previous behavior).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: event-driven reconciliation coalesces a burst of Docker events into a single pass.

## Impact
- **Code**: `DockYarp.Docker` — new `DebouncePolicy` (pure flush-delay math), `DockerDiscoveryOptions`
  gains `ReconcileDebounceMin`/`ReconcileDebounceMax`, `DockerDiscoveryService` drives the event loop
  through a `TimeProvider`-based coalescing pump; `AddDockerDiscovery` registers `TimeProvider.System`.
- **Tests (unit)**: `DebouncePolicy` flush-delay cases (quiet window, hard cap); `DockerDiscoveryService`
  coalesces a burst into one reconcile while a sparse event still reconciles.
- **Docs**: the site configuration reference gains `Docker:ReconcileDebounceMin`/`Max` (document-as-you-go).
- **Runtime / e2e**: live coalescing under real Docker events is deferred to the e2e batch; the debounce
  policy and the coalescing loop are unit-validated on Windows.
- **Owning agent**: AG-DD. Resolves `add-reconcile-debounce`.
