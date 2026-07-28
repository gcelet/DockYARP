## Why
Certificate provisioning runs **sequentially** (`ReconcileAsync` `foreach` + `await` per host), so one slow or
failing host's ACME validation (up to a 60 s timeout) blocks provisioning for **all other hosts** in that pass.
In production a single misconfigured `LETSENCRYPT_HOST` (bad DNS, unreachable challenge) delays issuance for
everyone on each reconcile pass; it also flaked the e2e `AcmeCertificate_IsProvisionedForHost` scenario (a slow
host pushed another host's cert past the test's poll).

## What Changes
- Provision hosts with **bounded concurrency** (`Parallel.ForEachAsync`, capped degree) instead of a sequential
  loop, keeping per-host failure isolation and flowing the `CancellationToken`. A slow/failing host no longer
  blocks the others.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: certificate provisioning is concurrent (bounded) and resilient to a single slow/failing host.

## Impact
- **Code**: `src/DockYarp.Tls/CertificateProvisioningService.cs`. `FileCertificateStore` is already thread-safe
  (a `Lock` guards its map; `Save` writes a per-host `.pfx`). Tests in `tests/DockYarp.Tls.Tests` (+ make the
  fake certificate store thread-safe).
- **Deferred**: making the concurrency degree / per-host validation timeout configurable (a bounded const for
  now); bumping the e2e poll budget.
- **Owning agent**: AG-AT.
