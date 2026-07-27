---
id: add-ipv6-support
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: ENABLE_IPV6 / PREFER_IPV6_NETWORK
provenance: this parity pass (matrix: IPv6 ⛔)
---

## Why
nginx-proxy can listen on IPv6 (`ENABLE_IPV6`) and prefer a backend's IPv6 address (`PREFER_IPV6_NETWORK`).
DockYarp listens on IPv4 and selects IPv4 backend addresses, so IPv6-only clients/backends aren't served.

## nginx-proxy behavior
- `ENABLE_IPV6` (default `false`): nginx listens on IPv6 as well.
- `PREFER_IPV6_NETWORK` (default `false`): prefer a container's IPv6 over IPv4 when both are reachable (never
  both, to avoid round-robin weight inflation).

## DockYarp today
Kestrel listener binding + the network address selector prefer IPv4 (`src/DockYarp.App`,
`src/DockYarp.Docker/Discovery/`). IPv6 listeners and IPv6 backend selection are ⛔.

## Proposed change (sketch)
- Listener: bind Kestrel to `[::]` in addition to IPv4 when enabled.
- Discovery: add a "prefer IPv6" toggle to the address selector, choosing one family deterministically.
- Update image/compose to publish IPv6 where relevant.

## Acceptance criteria (→ scenarios)
- **WHEN** IPv6 listening is enabled **THEN** an IPv6 client can reach a routed vhost.
- **WHEN** prefer-IPv6 is enabled and a backend has both families **THEN** the IPv6 address is selected (single
  family, no duplicate endpoints).

## Notes / risks / references
- Runtime/networking-heavy; validate in a Docker-capable session with an IPv6-enabled network.
