---
id: add-network-access-internal
capability: security
agent: AG-SEC
tier: A-structural
priority: medium
status: backlog
nginx-proxy: NETWORK_ACCESS=internal (+ network_internal.conf)
provenance: this parity pass (matrix: NETWORK_ACCESS=internal ⛔)
---

## Why
nginx-proxy can restrict a vhost to internal networks (`NETWORK_ACCESS=internal`), returning 403 to external
clients — common for admin/staging hosts. DockYarp has no per-host client-IP allow/deny.

## nginx-proxy behavior
- `NETWORK_ACCESS=internal` includes `network_internal.conf`, allowing a configurable set of private ranges
  (default `127/8,10/8,172.16/12,192.168/16`) and `deny all` for the rest (`nginx.tmpl:300`). The ranges file
  is mountable.

## DockYarp today
No client-IP-based access control per host. HTTPS enforcement, Basic Auth, and mTLS exist
(`src/DockYarp.Security/`), but nothing gates on source network.

## Proposed change (sketch)
Add a per-host `NETWORK_ACCESS=internal` label (+ a configurable internal-ranges list) enforced by middleware:
resolve the real client IP (respecting trusted forwarded headers / future PROXY protocol) and return 403 for
clients outside the internal ranges.

## Acceptance criteria (→ scenarios)
- **WHEN** a host sets `NETWORK_ACCESS=internal` and a client from a non-internal IP requests it **THEN** it
  gets 403.
- **WHEN** the client IP is within the configured internal ranges **THEN** it is served.
- **WHEN** the ranges are customized by config **THEN** enforcement uses the custom set.

## Notes / risks / references
- Correct client-IP resolution is essential (interacts with `TRUST_DOWNSTREAM_PROXY` and PROXY protocol).
