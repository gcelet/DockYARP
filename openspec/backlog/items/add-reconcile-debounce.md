---
id: add-reconcile-debounce
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: docker-gen -wait min:max (debounce)
provenance: 2026-07-31 parity re-analysis
---

## Why
docker-gen debounces rapid Docker events (`-wait min:max`) so a burst of container changes triggers one
regeneration instead of many. DockYarp reconciles on **every** event, which can cause redundant reconciles (and
store churn / YARP reloads) during rollouts where many containers start/stop at once.

## nginx-proxy behavior
- docker-gen `-wait min:max` coalesces events: after an event it waits `min`, extending up to `max`, then
  regenerates once for the whole burst.

## DockYarp today
- `DockerDiscoveryService` runs a full `ReconcileAsync` per lifecycle event (`WatchAsync` → reconcile). No
  coalescing window; a burst = a reconcile per event.

## Proposed change (sketch)
- Debounce the event-driven reconcile: on an event, wait a small `min` window (extending up to a `max`),
  collapsing a burst into a single reconcile. Configurable window with sensible defaults; startup and
  reconnect reconciles unaffected.

## Acceptance criteria (→ scenarios)
- **WHEN** several container events arrive within the debounce window **THEN** a single reconcile runs for the
  whole burst.
- **WHEN** events are sparse **THEN** each still reconciles promptly (within the window).

## Notes / risks / references
- Pure timing/coalescing logic around the existing reconcile; the debounce policy is unit-testable, the live
  event coalescing fits an e2e/integration check. Keep latency low (small default window).
