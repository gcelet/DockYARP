## 1. Domain model (AG-RP)

- [x] 1.1 Add endpoint value type (`readonly record struct` for destination address) in `DockYarp.Core/Models`
- [x] 1.2 Add cluster model (`sealed record`: id, endpoints, load-balancing policy enum, optional health-check config)
- [x] 1.3 Add route model (`sealed record`: host pattern, optional path prefix, priority, target cluster id, transform placeholder)
- [x] 1.4 Add per-host TLS metadata type (certificate host, contact email, enforce-HTTPS flag)
- [x] 1.5 Add `RouteConfigSnapshot` aggregate (immutable collections of routes + clusters, version)

## 2. Versioned configuration store (AG-RP)

- [x] 2.1 Define store interface in `DockYarp.Core/Interfaces` (get current snapshot, apply update)
- [x] 2.2 Implement snapshot store with atomic reference swap and monotonic `long` version
- [x] 2.3 Implement value-equality short-circuit so an identical update leaves the snapshot/version unchanged
- [x] 2.4 Ensure readers are lock-free (volatile/Interlocked read) and never observe a partial update

## 3. Host/path matching (AG-RP)

- [x] 3.1 Build the host index at snapshot construction (exact hosts ordinal-ignore-case + wildcard suffix list)
- [x] 3.2 Sort path prefixes by descending length per host
- [x] 3.3 Implement match(host, path) → route with exact-over-wildcard and longest-prefix rules, allocation-free on the hot path
- [x] 3.4 Return an explicit "no route" result when nothing matches

## 4. Configuration sources & precedence (AG-RP)

- [x] 4.1 Model configuration sources and an explicit precedence order (default: static > dynamic)
- [x] 4.2 Implement the merge that combines static + dynamic contributions into a snapshot
- [x] 4.3 Log conflicts (same host from two sources) with both sources identified, keeping the winner
- [x] 4.4 Skip and log individual invalid entries without dropping the rest of the source

## 5. Tests (AG-RP)

- [x] 5.1 Unit tests for the model (equality, cluster endpoint add/remove) with AwesomeAssertions
- [x] 5.2 Unit tests for the store (atomic swap under concurrent read, version increments, no-op no-churn)
- [x] 5.3 Unit tests for matching (exact vs wildcard, longest path prefix, no-match)
- [x] 5.4 Unit tests for merge/precedence (dynamic add, conflict resolution + logging, invalid-entry skip)

## 6. Documentation (AG-RP)

- [x] 6.1 Write `docs/routing-model.md` (model, matching rules, precedence, examples) replacing the stub
