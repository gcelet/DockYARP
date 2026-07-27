---
id: add-raw-ip-vhost
capability: docker-discovery
agent: AG-DD
tier: A-structural
priority: low
status: backlog
nginx-proxy: VIRTUAL_HOST (bare IP)
provenance: this parity pass (untracked feature; nginx test_raw-ip-vhost)
---

## Why
nginx-proxy accepts a bare IP address as `VIRTUAL_HOST` (covered by its `test_raw-ip-vhost`). DockYarp treats
`VIRTUAL_HOST` purely as a DNS host; an IP value may not match/route as expected. Small but real parity gap
for IP-addressed access.

## nginx-proxy behavior
- `VIRTUAL_HOST` may be a raw IPv4/IPv6 address; nginx routes by matching the `Host` header (the IP).

## DockYarp today
`VIRTUAL_HOST` is comma-split, trimmed, case-insensitively de-duplicated (`LabelParser.cs:255-269`) and used
as a host match in `RouteMatcher`. An IP literal is not special-cased; confirm whether host matching handles a
bare IP `Host` header correctly.

## Proposed change (sketch)
Verify and, if needed, adjust host normalization/matching so an IP-literal `VIRTUAL_HOST` matches requests
whose `Host` header is that IP (with/without port). Add a routing test.

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_HOST=192.0.2.10` and a request arrives with `Host: 192.0.2.10` **THEN** it routes to that
  container.
- **WHEN** the `Host` header carries a port (`192.0.2.10:8080`) **THEN** matching still succeeds.

## Notes / risks / references
- Likely a small change + a test; verify current behavior before spec'ing the delta.
