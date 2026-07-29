---
id: e2e-multi-network
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: multiple networks / unreachable-network resilience (runtime validation)
provenance: deferred from add-multi-network-attachment, 2026-07-29
---

## Why
`add-multi-network-attachment` made address selection reachability-aware via a configured `Docker:ProxyNetworks`
set and unit-tested the pure logic, but two runtime aspects were deferred: (1) auto-detecting the proxy's own
network memberships so `ProxyNetworks` need not be configured, and (2) validating the live skip behavior against
a real daemon with a backend on an unreachable network.

## nginx-proxy behavior
- The proxy attaches to multiple networks and picks a reachable backend IP, degrading gracefully for containers
  on unreachable networks (`test_multiple-networks`, `test_vhost-in-multiple-networks`, `test_unreachable-network`).

## DockYarp today
- Reachability-aware selection is implemented and unit-tested, but gated on a **configured** `Docker:ProxyNetworks`
  (`add-multi-network-attachment`). The proxy's own networks are not auto-detected, and the skip path is not
  exercised end to end.

## Proposed change (sketch)
- Auto-detect the proxy container's own networks at startup (Docker self-inspection via the container id /
  hostname) and use them as the default `ProxyNetworks` when none is configured.
- Add an Aspire e2e scenario with a backend on a network the proxy does not share, asserting it is skipped (no
  route, warning logged), and a multi-network backend that resolves to the shared network.

## Acceptance criteria (→ scenarios)
- **WHEN** `ProxyNetworks` is not configured **THEN** the proxy's own attached networks are used as the reachable set
- **WHEN** a backend is only on an unreachable network **THEN** it is not routed and a warning is logged (e2e)

## Notes / risks / references
- Requires a Docker-capable session; validate self-inspection (HOSTNAME → container id) across compose/Swarm.
- Sibling (done): `add-multi-network-attachment` (config-driven reachability logic).
- Related: `add-host-network-mode` (also needs live host/network validation).
