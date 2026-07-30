---
id: e2e-host-network-mode
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: host network mode backends (runtime validation)
provenance: deferred from add-host-network-mode, 2026-07-30
---

## Why
`add-host-network-mode` routes host-network backends to a configured `Docker:HostAddress` and unit-tested the
detection + address logic, but did not validate that the configured host address is actually reachable from
inside the proxy container. That reachability is platform-specific and needs a live check.

## nginx-proxy behavior
- Host-network backends are supported; they must set `VIRTUAL_PORT`, and nginx targets the host address on that
  port (`test_host-network-mode`).

## DockYarp today
- `Docker:HostAddress` + host-mode detection are implemented and unit-tested (`add-host-network-mode`). No live
  round-trip proves the proxy can reach a host-network backend.

## Proposed change (sketch)
- Add an Aspire e2e scenario with a backend published on the host network, labeled `VIRTUAL_HOST` +
  `VIRTUAL_PORT`, and assert a request through DockYarp reaches it.
- Exercise the platform wiring: `host.docker.internal` on Docker Desktop vs Linux
  (`--add-host host.docker.internal:host-gateway` or the host-gateway IP).
- Assert the skip-with-warning path when `Docker:HostAddress` is unset.

## Acceptance criteria (→ scenarios)
- **WHEN** a host-network backend is labeled and `Docker:HostAddress` is set **THEN** a request through DockYarp
  reaches it end to end.
- **WHEN** `Docker:HostAddress` is unset **THEN** the host-network backend is not routed and a warning is logged.

## Notes / risks / references
- Requires a Docker-capable session; validate `host.docker.internal` availability per platform.
- Sibling (done): `add-host-network-mode` (detection + address logic).
- Related: `e2e-multi-network` (also needs live network validation) — consider batching.
