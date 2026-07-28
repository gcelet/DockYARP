---
id: parallelize-certificate-provisioning
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: e2e diagnostics log review, 2026-07-28 (dockyarp.log; AcmeCertificate_IsProvisionedForHost flake)
---

## Why
Certificate provisioning is **sequential**, so a single slow or failing host's ACME validation (up to a 60 s
timeout) blocks/delays provisioning for **every other host** in that pass. In production, one misconfigured
`LETSENCRYPT_HOST` (bad DNS, unreachable HTTP-01 challenge) delays certificate issuance for all other hosts on
each reconcile pass. It also makes the e2e `AcmeCertificate_IsProvisionedForHost` scenario flaky.

## Observed behavior (e2e log)
On a slow run, `hsts.local` hit the 60 s validation timeout
(`fail: DockYarp.Tls.CertificateProvisioningService[2] … System.TimeoutException: Timed out waiting for ACME
authorization`), which delayed the rest of the pass; `tls.local`'s certificate was then provisioned ~2 s
**after** the test's 60 s poll (`TlsPollSeconds`) expired, so the test observed the self-signed fallback
(`CN=dockyarp.local`) instead of the ACME cert (`DockYarp E2E`) and failed.

## DockYarp today
- `CertificateProvisioningService.ReconcileAsync` (`src/DockYarp.Tls/CertificateProvisioningService.cs:37-59`)
  iterates `TlsDomains.Desired(snapshot)` in a `foreach`, `await`ing each host's `RequestCertificateAsync`
  one at a time. Per-host failures are isolated (try/catch, `CA1031` suppressed with justification) — but the
  sequential `await` causes **head-of-line blocking**.
- `CertesAcmeClient.WaitForValidationAsync` (`src/DockYarp.Tls/CertesAcmeClient.cs:74-92`) polls the ACME
  authorization **30 × 2 s = 60 s**, then throws `TimeoutException`.

## Proposed change (sketch)
- Provision hosts with **bounded concurrency** instead of a sequential loop: e.g. `Parallel.ForEachAsync` or a
  `SemaphoreSlim`-gated `Task.WhenAll` over `TlsDomains.Desired(...)`, keeping the existing per-host try/catch
  isolation and flowing the `CancellationToken`. A slow/failing host no longer delays the others.
- **Bound** the parallelism (a small degree, e.g. 4–8) so DockYarp does not hammer the ACME authority and trip
  rate limits.
- Secondary (optional): add a short back-off between validation polls, and bump the e2e `TlsTests.TlsPollSeconds`
  as belt-and-suspenders.

## Acceptance criteria (→ scenarios)
- **WHEN** several hosts need certificates and one host's ACME validation is slow or times out
- **THEN** the other hosts are still provisioned promptly (not blocked behind the slow one)
- **WHEN** a host's provisioning fails
- **THEN** it is logged and does not affect the other hosts (existing isolation preserved)
- **WHEN** many hosts need certificates at once
- **THEN** concurrent ACME requests stay within a bounded degree of parallelism
- **(e2e)** the `AcmeCertificate_IsProvisionedForHost` scenario is reliably green

## Notes / risks / references
- Internal robustness + test-reliability finding — no `parity.md` row.
- Watch ACME rate limits (Let's Encrypt) — hence bounded, not unbounded, concurrency.
- Add a unit test for the provisioning service asserting a slow/failing host does not block a fast one (fake
  ACME client with a delayed/failing host).
