---
id: add-multi-network-attachment
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: multiple networks / unreachable-network resilience
provenance: this parity pass (matrix: multi-network ⛔)
---

## Why
nginx-proxy reaches backends across several Docker networks and tolerates containers on networks it cannot
reach. DockYarp selects one preferred network; backends only on other networks may be unreachable, and
unreachable-network handling isn't specified.

## nginx-proxy behavior
- The proxy attaches to multiple networks (`docker network connect`) and picks a reachable backend IP; it
  degrades gracefully for containers on unreachable networks (`test_multiple-networks`,
  `test_vhost-in-multiple-networks`, `test_unreachable-network`).

## DockYarp today
Network selection is deterministic with a single preferred network + skip-ingress
(`src/DockYarp.Docker/Discovery/` network selector). Multi-network reachability and explicit
unreachable-network resilience are ⛔.

## Proposed change (sketch)
Enumerate all networks a container is attached to, choose the first reachable one relative to the proxy's own
networks (deterministic order), and skip/log backends with no reachable address instead of producing a broken
endpoint.

## Acceptance criteria (→ scenarios)
- **WHEN** a backend is attached to several networks and the proxy shares one **THEN** the shared, reachable
  address is selected.
- **WHEN** a backend is only on a network the proxy cannot reach **THEN** it is skipped with a warning (no
  broken route).

## Notes / risks / references
- Determining "reachable" requires knowing the proxy's own network memberships; validate in a Docker-capable
  session.
