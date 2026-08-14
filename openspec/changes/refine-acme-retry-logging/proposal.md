## Why
The E2E run (2026-08-06, Docker Desktop) logged 4× `fail: CertificateProvisioningService[2] Failed to provision
certificate for {host} — System.TimeoutException: Timed out waiting for ACME authorization`, **at Error level with
full stack traces**, even though the immediate retry succeeded ~150 ms later. The root cause is a startup timing race
(step-ca validates the HTTP-01 challenge before the proxy is serving it). The Error+stacktrace noise is misleading —
it looks like a real failure, and could mask genuine errors or trip log-based alerts — while certificates actually
provision correctly.

## What Changes
- Track a **per-host consecutive-failure counter** in `CertificateProvisioningService`. The first few consecutive
  provisioning failures for a host (a transient failure, resolved by the reconcile loop's retry) are logged at
  **Warning** with a short reason and **no stack trace**; once failures persist past the threshold, the log escalates
  to **Error** with the exception (a genuine, persistent failure stays loud).
- A success resets the host's counter, so a later transient failure is treated as transient again.
- New source-generated log `TlsLog.ProvisioningRetrying` (Warning, no exception argument) alongside the existing
  `ProvisioningFailed` (Error, with exception).

Option B from the backlog sketch (gate ACME validation on challenge readiness to reduce the race at the source) is
**deliberately out of scope**: it is more invasive and timing-coupled, and option A already satisfies both acceptance
criteria for any transient failure, not just the startup race.

## Capabilities
### Modified Capabilities
- `tls-acme`: provisioning-failure logging distinguishes transient (Warning) from persistent (Error) failures.

## Impact
- **Code**: `DockYarp.Tls` — `CertificateProvisioningService` (per-host counter + level decision), `TlsLog`
  (`ProvisioningRetrying`). No new configuration key, no behavior a user configures — internal observability only.
- **Tests**: `DockYarp.Tls.Tests` — a fail-then-succeed host logs Warning (not Error); a persistently failing host
  escalates to Error after the threshold; a success resets the counter. Reduced noise is confirmable in
  `artifacts/e2e-logs/dockyarp.log` (the transient timeouts become Warnings).
- **Docs**: none — no user-facing label/env var/config key/workflow changes (log-level refinement is internal).
- **Owning agent**: AG-AT.
