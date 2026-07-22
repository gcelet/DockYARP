## Context

`/api/certs` returns an empty stub and `/api/health` returns a hard-coded `"Healthy"`. Making them real
means observing other modules (certificates from `Tls`, discovery connectivity from `Docker`) — but
`AdminApi` should stay decoupled (it references only `Core` today).

## Goals / Non-Goals

**Goals:** `/api/certs` reflects the certificate store; `/api/health` reflects real signals (route/cluster
counts, certificate count, discovery connectivity) and degrades when discovery is enabled but disconnected.

**Non-Goals:** request/latency metrics (separate backlog); backend health aggregation.

## Decisions

- **AdminApi owns consumer abstractions; App provides adapters** (dependency inversion — no `AdminApi→Tls`
  or `AdminApi→Docker` coupling):
  - `ICertificateInventory` → `IReadOnlyList<CertView>` (AdminApi's sanitized DTO). App's
    `CertificateInventoryAdapter` maps `ICertificateStore.List()` (Tls) to `CertView`.
  - `IDiscoveryHealth` (`Enabled`, `Connected`). App wires it: a `DiscoveryHealthAdapter` over a new
    `DiscoveryHealthState` when discovery is enabled, or a disabled instance otherwise.
- **`DiscoveryHealthState`** (in `DockYarp.Docker`, registered by `AddDockerDiscovery`): a small
  thread-safe connected flag the `DockerDiscoveryService` sets `true` after a successful connect/reconcile
  and `false` on failure/disconnect. Internal enabler — no `docker-discovery` spec change.
- **`/api/health`** computes: `Degraded` when `IDiscoveryHealth.Enabled && !Connected`, else `Healthy`;
  body reports route/cluster/certificate counts and a discovery status (`connected`/`disconnected`/`disabled`).
- **`/api/certs`** returns `ICertificateInventory.List()` (host + expiry, no private keys).

## Risks / Trade-offs

- The discovery-connected signal is coarse (connected/at-least-one-successful-cycle vs failing) — enough for
  a health status; not a full probe. Documented.

## Migration Plan

Additive: new abstractions + adapters + a discovery health flag; endpoints read real data.

## Open Questions

- Whether to protect `/metrics` or expose latency metrics — deferred to a metrics-focused change.
