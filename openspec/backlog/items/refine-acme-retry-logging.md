---
id: refine-acme-retry-logging
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal — observability)
provenance: 2026-08-06 E2E log review (Docker Desktop) — transient ACME failures logged as Error+stacktrace
---

## Why
The E2E run (2026-08-06, Docker Desktop) logged 4× `fail: CertificateProvisioningService[2] Failed to provision
certificate for {host} — System.TimeoutException: Timed out waiting for ACME authorization`, **with full stack
traces at Error level**, even though the **immediate retry succeeded** (`Provisioned certificate for {host}` ~150 ms
later). Root cause is a **startup timing race**: step-ca tried to validate the HTTP-01 challenge before the proxy was
serving it (step-ca log: `acme:error:connection — "The server could not connect to validation target"`). The
Error+stacktrace noise is misleading (looks like a real failure), and could mask genuine errors or trip log-based
alerts. This is cosmetic — certificates provision correctly — but worth cleaning up.

## Current state
- `CertificateProvisioningService.ReconcileAsync` logs every provisioning exception at **Error** with the exception
  (`src/DockYarp.Tls/CertificateProvisioningService.cs:60`), then the reconcile loop retries and succeeds.
- `CertesAcmeClient.WaitForValidationAsync` (`src/DockYarp.Tls/CertesAcmeClient.cs:92`) throws `TimeoutException`
  when the authorization doesn't become valid within the wait window; called from `RequestCertificateAsync`
  (`CertesAcmeClient.cs:37`).

## Proposed change (sketch — decide in design)
- **A. Downgrade transient failures:** log the first N consecutive provisioning failures for a host at **Warning**
  (short message, no stack trace), and escalate to **Error** only after N attempts (a persistent failure). Track a
  per-host attempt counter in the provisioning service.
- **B. Readiness before validation:** don't trigger ACME validation until the challenge is actually servable (the
  HTTP-01 token is stored and the challenge path responds), so step-ca doesn't hit "could not connect to validation
  target" on the first try — reducing/eliminating the transient timeout at the source.
- Possibly **both** (A for resilience to any transient, B to reduce occurrence).

## Acceptance criteria (→ scenarios)
- **WHEN** an ACME provisioning attempt fails transiently and a later attempt succeeds **THEN** the transient failure
  is logged at Warning (no misleading Error/stack trace for the first attempt).
- **WHEN** provisioning keeps failing (≥ N attempts) **THEN** it escalates to Error with the exception.

## Notes / risks / references
- Unit-testable: the attempt-count → log-level policy, and the readiness check, are pure/testable; the reduced noise
  is confirmable in the E2E logs (`artifacts/e2e-logs/dockyarp.log`) — the 4 transient timeouts should become
  Warnings and/or disappear.
- Keep genuine, persistent failures loud. Refs: `CertificateProvisioningService.cs:60`, `CertesAcmeClient.cs:37,92`.
