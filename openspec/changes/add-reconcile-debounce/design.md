# Design — add-reconcile-debounce

## Scope
Only the **event-driven** reconcile is debounced. The startup pass and the post-reconnect pass in
`DockerDiscoveryService.ExecuteAsync` run a full `ReconcileAsync` immediately and are left untouched — they
must converge state without waiting. Debouncing wraps only the inner event stream.

## Policy (pure, unit-tested — mirrors `BackoffPolicy`)
The coalescing decision is a pure function of how long the current burst has already lasted:

```
DebouncePolicy.ComputeFlushDelay(sinceBurstStart, min, max):
    capRemaining = max - sinceBurstStart
    if capRemaining <= 0 : return TimeSpan.Zero        // hard cap reached -> flush now
    return capRemaining < min ? capRemaining : min     // quiet window, clamped by the cap
```

The loop recomputes this **right after consuming an event**, so `min` is the quiet window measured from the
most recent event (each event extends it), while `max - sinceBurstStart` guarantees a burst that never
pauses is still flushed at the cap rather than deferred indefinitely. `min = 0` returns `Zero` every time,
i.e. reconcile per event (previous behavior).

## Coalescing pump (`TimeProvider`-driven)
The event loop consumes the `IContainerSource.WatchAsync` enumerator directly (rather than `await foreach`)
so it can *wait for the next event but give up after the debounce window*:

```
PumpEventsAsync:
  read = first MoveNextAsync
  while (await read):                     // first event of a burst (or stream end)
      CoalesceBurstAsync                  // drain further events within the window
      ReconcileAsync                      // one reconcile for the whole burst

CoalesceBurstAsync:
  start = timeProvider.GetTimestamp()
  loop:
     wait = DebouncePolicy.ComputeFlushDelay(GetElapsedTime(start), min, max)
     if wait <= 0: return                 // cap -> flush
     read = MoveNextAsync
     if WhenAny(read, Delay(wait, timeProvider)) == delay:
         bufferedRead = read; return      // quiet window elapsed -> flush, carry the pending read
     if read completed false:
         bufferedRead = read; return      // stream ended -> flush, carry (false) so the pump exits
     // else a new event arrived within the window -> loop (extend, bounded by the cap)
```

**The carried read.** A pull-based enumerator cannot cancel a `MoveNextAsync` already in flight, so when the
quiet window elapses the pending read is stashed in `bufferedRead` and consumed as the next burst's first
event — no event is dropped or double-counted. Buffered events (a rollout's burst is already queued in the
event channel) complete synchronously, so they drain into one burst without ever arming the delay.

`Task.Delay(wait, timeProvider, ct)` and `GetElapsedTime` share the injected `TimeProvider`, so the loop is
consistent and deterministically drivable; production uses `TimeProvider.System` (registered by
`AddDockerDiscovery`).

## Options and defaults
`DockerDiscoveryOptions.ReconcileDebounceMin` (default 250 ms) and `ReconcileDebounceMax` (default 2 s), bound
from the `Docker` section like the existing reconnect delays. 250 ms is imperceptible for routing yet collapses
a rollout burst; the 2 s cap bounds worst-case latency for a burst that never settles.

## Non-goals
- Live coalescing under real Docker events is an integration/e2e concern (batched: `e2e-*`), not part of this
  Windows-validatable change.
- No change to startup/reconnect reconciliation, to the reconciler itself, or to what an event triggers.
