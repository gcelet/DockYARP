---
id: add-host-network-mode
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: host network mode backends
provenance: this parity pass (matrix: host-mode ⛔)
---

## Why
nginx-proxy supports backends running in Docker **host** network mode (and running itself in host mode).
DockYarp's address selection assumes a container network IP, so host-mode backends aren't reachable.

## nginx-proxy behavior
- Host-network backends are supported; they **must** set `VIRTUAL_PORT` (there is no container IP to infer),
  and nginx targets the host address on that port (`test_host-network-mode`).

## DockYarp today
Network address selection picks a container IP from a preferred/non-ingress network
(`src/DockYarp.Docker/Discovery/` + network selector); a host-mode container exposes no such network IP, so it
is effectively unroutable (matrix ⛔).

## Proposed change (sketch)
Detect host-network containers (empty/`host` network settings) and, when `VIRTUAL_PORT` is set, target the
Docker host address (gateway / configured host) on that port. Require `VIRTUAL_PORT` and warn if missing.

## Acceptance criteria (→ scenarios)
- **WHEN** a host-network backend sets `VIRTUAL_HOST` + `VIRTUAL_PORT` **THEN** DockYarp routes to the host
  address on that port.
- **WHEN** a host-network backend omits `VIRTUAL_PORT` **THEN** it is skipped with a clear warning.

## Notes / risks / references
- Determining the reachable host address from inside the proxy container is the tricky part; validate in a
  Docker-capable session.
