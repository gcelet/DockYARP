---
id: test-restart-state-persistence
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: deferred from persist-state-on-writable-volume, 2026-07-28
---

## Why
`persist-state-on-writable-volume` made DockYarp write certificates + Data Protection keys to a non-root-writable
volume, and the 2026-07-28 e2e run proved the **write** path (certs provisioned, no permission error). It did
**not** prove the **survives-recreation** half of the requirement: the spec scenario "State survives container
recreation" is currently unverified by any automated test.

## nginx-proxy behavior
N/A — internal test-coverage finding for a DockYarp-specific runtime property. No `parity.md` row.

## DockYarp today
- Deployment spec (archived `persist-state-on-writable-volume`) declares:
  `#### Scenario: State survives container recreation` — provisioned state must still be present after the
  container is recreated against the same volume.
- The e2e suite bind-mounts a world-writable `/certs` (`TlsHarness.PrepareCertsDirectory`) but never recreates
  the DockYarp container within a run, so persistence-across-restart is asserted only by the spec, not a test.

## Proposed change (sketch)
1. Add one e2e scenario (integrated into the existing TLS AppHost, not a synthetic new suite — per the testing
   pyramid): provision a cert for a host, then **recreate** the DockYarp container against the same `/certs`
   mount, and assert the previously issued certificate (and the DP key ring) is reused rather than re-provisioned.
2. Prefer restarting/replacing the `dockyarp` resource over standing up a second AppHost, to keep the run cheap.
   Verify the Aspire restart/replace API against the aspire MCP/skills before implementing (APIs move fast).

## Acceptance criteria (→ scenarios)
- **WHEN** DockYarp has provisioned a certificate to the mounted volume and the container is recreated against
  the same volume
- **THEN** the certificate (and Data Protection key ring) is still present and reused — not re-provisioned from
  the ACME authority

## Notes / risks / references
- Internal finding — no `parity.md` row.
- Batched/smart e2e: fold into an existing TLS scenario; do not add a one-off suite (testing-pyramid convention).
- Verify the Aspire container restart/recreate API via the aspire MCP first.
- Sibling (done): `persist-state-on-writable-volume`.
