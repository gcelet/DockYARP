---
id: e2e-stepca-long-cert-duration
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: deferred from test-restart-state-persistence, 2026-07-28
---

## Why
The e2e uses `Tls__RenewBeforeExpiry=00:01:00` so that step-ca's ~24h certificates are never "near expiry" during
a run, which keeps the served certificate thumbprint stable for `RestartPersistenceTests`. That works, but it
tweaks a **product** option to an unrealistic value purely for the test. The more coherent fix keeps DockYarp's
realistic default renewal margin (30 days) and instead makes the **test CA** issue certificates longer than that
margin, so no renewal is due during a run.

## nginx-proxy behavior
N/A — internal e2e-infrastructure coherence improvement. No `parity.md` row.

## DockYarp today
- `tests/DockYarp.E2E.AppHost/Program.cs` sets `Tls__RenewBeforeExpiry=00:01:00` on the `dockyarp` container
  (added by `test-restart-state-persistence`) to avoid renewal churn.
- step-ca (`smallstep/step-ca`) issues ~24h ACME certificates by default; DockYarp's default `RenewBeforeExpiry`
  is 30 days, so without the override DockYarp renews on every `CheckInterval` (5s) pass.

## Proposed change (sketch)
Make step-ca issue certificates longer than 30 days (e.g. 60d) via the ACME provisioner's
`authority.claims` (`defaultTLSCertDuration` / `maxTLSCertDuration`) in `ca.json`, then drop the
`Tls__RenewBeforeExpiry` override so DockYarp runs with its production default.
- step-ca exposes **no env** for cert duration; `ca.json` is generated at init and read only at startup (no
  hot-reload without `SIGHUP`, which is not deliverable across containers under DCP).
- Options: split step-ca **init** (create PKI + patch `ca.json` claims) from **serve** so the served config
  already has long durations; or patch `ca.json` and restart step-ca **before** DockYarp first provisions. The
  minimal image has no `jq`, so a patch step needs a `jq`-capable helper (or patch from the host in the fixture).
- Must not destabilize the working ACME chain (root/intermediate + `ca-bundle`); validate via an `E2E` run.

## Acceptance criteria (→ scenarios)
- **WHEN** the e2e provisions a certificate
- **THEN** step-ca issues it with a duration longer than DockYarp's default renewal margin, so no renewal occurs
  during a run
- **WHEN** `RestartPersistenceTests` runs with DockYarp's default `RenewBeforeExpiry` (override removed)
- **THEN** the served certificate thumbprint is stable and the restart-reuse assertion holds

## Notes / risks / references
- Internal finding — no `parity.md` row.
- Sibling (done): `test-restart-state-persistence` (uses the `RenewBeforeExpiry` lever instead).
- Cannot be validated locally (Docker only in the e2e environment); iterate via `E2E` runs.
