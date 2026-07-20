## Context

`DockYarp.Core` is the dependency-free leaf of the solution. It must hold the routing state that
Docker discovery writes and that the YARP adapter reads, updated live as containers come and go. The
hot read path (per-request matching) must be low-allocation and lock-free; writes are comparatively
rare (container lifecycle events). YARP has its own config types, but `Core` must not depend on YARP —
the adapter in a later change bridges the two.

## Goals / Non-Goals

**Goals:**
- A YARP-independent, immutable routing model (routes, clusters, endpoints, per-host TLS metadata).
- A store publishing atomic immutable snapshots with a monotonic version, safe under concurrent reads.
- Deterministic host/path matching (exact vs wildcard host, longest path prefix, priority).
- A defined merge/precedence between static and dynamic configuration sources.

**Non-Goals:**
- No YARP `IProxyConfigProvider` adapter (→ `add-yarp-dynamic-config`).
- No Docker client or label parsing (→ `add-docker-discovery`).
- No load-balancing execution or health-check probing (policy is modeled here; execution is YARP's).
- No certificate acquisition (only TLS *metadata* is modeled here).

## Decisions

- **Immutable snapshot + reference swap** over lock-based mutation. A `RouteConfigSnapshot` holds
  `ImmutableArray`/frozen collections; the store swaps the reference with `Interlocked.Exchange` (or
  `volatile` read + lock on write). Rationale: readers are lock-free and always see a consistent graph;
  writes are infrequent. Alternative considered: `ReaderWriterLockSlim` — rejected for hot-path overhead.
- **Records for the model** (`readonly record struct` for small value types like an endpoint address,
  `sealed record` for aggregates). Rationale: value equality enables cheap "did anything change?" checks
  to implement the no-op-update scenario and avoid churning readers.
- **Precompiled matching structure.** Build a host index at snapshot-build time (exact hosts in a
  dictionary keyed with `StringComparer.OrdinalIgnoreCase`; wildcard suffixes in a small ordered list).
  Path prefixes sorted by descending length per host. Rationale: keeps per-request matching allocation-free.
- **Monotonic version** is a `long` incremented only when the newly built snapshot differs by value from
  the current one, satisfying "no-op update does not churn readers".
- **Source precedence** modeled as an explicit enum/order (e.g. static > dynamic, configurable), with a
  merge function that records conflicts through `ILogger` rather than throwing.

## Risks / Trade-offs

- Rebuilding the whole snapshot on every change → simplest and correct; container churn is low-volume, so
  the cost is acceptable. Mitigation: value-equality short-circuit avoids publishing identical snapshots.
- Wildcard matching scope kept to single-level subdomain (`*.local`) for now → documented; multi-level
  patterns are out of scope until a requirement needs them.

## Migration Plan

Greenfield: purely additive in `DockYarp.Core`. No data migration, no rollback concerns.

## Open Questions

- Exact precedence default (static-wins vs last-writer) — start with static-wins, revisit when
  `add-configuration`/`add-docker-discovery` land.
- Whether transforms are modeled now or deferred — model a minimal transform placeholder, expand in
  `add-yarp-dynamic-config`.
