# Design — refine-acme-retry-logging

## Policy: transient vs persistent
`CertificateProvisioningService.ReconcileAsync` runs a bounded-parallel pass over the desired hosts; each host's
provisioning is wrapped in a `try/catch`. Today every caught exception logs at **Error** with the exception
(`TlsLog.ProvisioningFailed`), so a transient startup-race timeout that the very next pass resolves looks like a hard
failure.

Introduce a **per-host consecutive-failure counter** and decide the log level from it:

```csharp
private readonly ConcurrentDictionary<string, int> consecutiveFailures = new(StringComparer.OrdinalIgnoreCase);

// success:
consecutiveFailures.TryRemove(desired.Host, out _);
TlsLog.CertificateProvisioned(logger, desired.Host);

// catch (Exception exception):  (OperationCanceledException still rethrows, unchanged)
int attempt = consecutiveFailures.AddOrUpdate(desired.Host, 1, static (_, prev) => prev + 1);
if (attempt <= TransientFailureThreshold)
{
    TlsLog.ProvisioningRetrying(logger, desired.Host, attempt, Describe(exception));
}
else
{
    TlsLog.ProvisioningFailed(logger, desired.Host, exception);
}
```

- `ConcurrentDictionary` because the pass provisions hosts concurrently (each host is a distinct key within a pass, so
  there is no same-key contention, but the map itself must be thread-safe).
- `TransientFailureThreshold` is a small named constant (`2`): the first two consecutive failures for a host are
  Warnings; the third and beyond are Errors. This silences the single startup-race timeout (which resolves on the next
  pass) while a genuinely stuck host still escalates. Kept internal (not a config key) to avoid growing the public
  option surface for an observability nuance.
- `Describe(exception)` yields a short one-line reason (the exception message) — no stack trace at Warning; the full
  exception is preserved for the Error escalation.

## Logging
Add one source-generated message; keep the existing Error one for escalation:

```csharp
[LoggerMessage(EventId = 5, Level = LogLevel.Warning,
    Message = "Provisioning certificate for {Host} did not succeed yet (attempt {Attempt}); will retry: {Reason}")]
public static partial void ProvisioningRetrying(ILogger logger, string host, int attempt, string reason);
```

`ProvisioningRetrying` takes **no** `Exception` argument, so the logger does not render a stack trace — the misleading
noise. `ProvisioningFailed` (EventId 2, Error, with the exception) is unchanged and is used only on escalation.

## Why not option B (readiness gate)
Gating ACME validation on the challenge path actually responding would reduce the race at the source, but it reaches
into the Certes order/validation flow, is timing-coupled, and is hard to unit-test deterministically. Option A handles
**any** transient failure (not just the startup race) and fully meets the acceptance criteria, so B is left out.

## Tests (`DockYarp.Tls.Tests`)
Using a capturing `ILogger` (records level + event id):
- **Transient**: an ACME client that throws once for a host then succeeds → the failure is logged at **Warning**
  (EventId 5, no exception), and the host ends up provisioned; the counter is cleared on success.
- **Persistent**: an ACME client that always throws → the first `TransientFailureThreshold` attempts (across passes)
  log Warning, the next logs **Error** (EventId 2, with the exception).
- **Reset**: fail once (Warning), succeed (cleared), fail again → Warning again, not Error.
