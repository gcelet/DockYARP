## Why

Every other capability (Docker discovery, YARP dynamic config, TLS, security, Admin API) reads or
writes DockYarp's routing state. Nothing can be built until that state has a well-defined,
YARP-independent domain model with safe concurrent updates. This change establishes that foundation in
`DockYarp.Core` so later changes map into it instead of inventing ad-hoc shapes.

## What Changes

- Introduce the internal **routing domain model** in `DockYarp.Core` (route, cluster, endpoint,
  per-host TLS metadata), independent from YARP types.
- Add a **thread-safe, versioned configuration store** exposing atomic, immutable snapshots (no torn
  reads, monotonic version) that can be swapped at runtime without a process restart.
- Add **host/path matching** (exact host, wildcard subdomain, longest path prefix, priority).
- Define **configuration sources and precedence** so routes/clusters can come from static config and
  from dynamic sources (Docker) with deterministic conflict resolution and logging.
- No YARP wiring, no Docker client, no HTTP pipeline here — those consume this model in later changes.

## Capabilities

### New Capabilities
- `proxy-routing`: the internal routing model (routes, clusters, endpoints, per-host TLS metadata),
  the versioned snapshot store, host/path matching rules, and multi-source configuration precedence.

### Modified Capabilities
<!-- None: greenfield foundation. -->

## Impact

- **Code**: new types in `src/DockYarp.Core` (`Models/`, `Stores/`, `Interfaces/`); unit tests in
  `tests/DockYarp.Core.Tests`.
- **Dependencies**: none beyond the BCL (keeps `Core` a dependency-free leaf). No YARP package yet.
- **Downstream**: unblocks `docker-discovery` (maps labels into this model) and `yarp-dynamic-config`
  (adapts this model to YARP `IProxyConfigProvider`).
- **Owning agent**: AG-RP.
