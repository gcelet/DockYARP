## 1. Debounce policy (AG-DD)
- [x] 1.1 `DebouncePolicy.ComputeFlushDelay(sinceBurstStart, min, max)` — pure static: `Zero` at/after the
      cap, else `min` clamped by `max - sinceBurstStart`

## 2. Options (AG-DD)
- [x] 2.1 `DockerDiscoveryOptions.ReconcileDebounceMin` (default 250 ms) and `ReconcileDebounceMax` (default 2 s)

## 3. Coalescing pump (AG-DD)
- [x] 3.1 `DockerDiscoveryService` takes a `TimeProvider`; `ExecuteAsync` delegates the event stream to a
      debounced pump (startup + reconnect reconciles stay immediate)
- [x] 3.2 `PumpEventsAsync` + `CoalesceBurstAsync`: consume the `WatchAsync` enumerator, coalesce a burst via
      `DebouncePolicy` + `Task.Delay(wait, timeProvider, ct)`, carry the pending read across a flush
- [x] 3.3 `AddDockerDiscovery` registers `TimeProvider.System` (TryAddSingleton)

## 4. Tests (AG-DD)
- [x] 4.1 `DebouncePolicyTests`: sparse → `min`; near the cap → the remaining cap; at/after the cap → `Zero`
- [x] 4.2 `DockerDiscoveryServiceTests`: a burst of events coalesces into a single reconcile (list call count);
      a sparse event still reconciles. Update `CreateService` to pass `TimeProvider.System` + a small window

## 5. Docs (AG-DOC)
- [x] 5.1 Site configuration reference: document `Docker:ReconcileDebounceMin`/`Max` (defaults; `Min=0` disables)

## 6. Verify (AG-DD)
- [x] 6.1 Nuke `Test` gate green (unit/integration, no Docker)
