## 1. Logging (AG-AT)
- [x] 1.1 `TlsLog`: add `ProvisioningRetrying` (EventId 5, Warning, `{Host}`/`{Attempt}`/`{Reason}`, **no** Exception
  argument so no stack trace)

## 2. Failure policy (AG-AT)
- [x] 2.1 `CertificateProvisioningService`: per-host `ConcurrentDictionary<string,int>` consecutive-failure counter;
  increment on failure, remove on success
- [x] 2.2 On failure, log at Warning (`ProvisioningRetrying`) while `attempt <= TransientFailureThreshold`, else
  escalate to `ProvisioningFailed` (Error, with exception); `OperationCanceledException` still rethrows unchanged
- [x] 2.3 `TransientFailureThreshold` named constant (2), with a short justifying comment

## 3. Tests (AG-AT)
- [x] 3.1 `DockYarp.Tls.Tests`: transient (fail-once-then-succeed) logs Warning (EventId 5), host provisioned
- [x] 3.2 Persistent failure escalates to Error (EventId 2) after the threshold
- [x] 3.3 A success resets the counter (fail → succeed → fail logs Warning again, not Error)

## 4. Verify (AG-AT)
- [x] 4.1 Nuke `Test` gate green (unit), warnings-as-errors clean
- [x] 4.2 (Confirmable, not required in the gate) E2E `dockyarp.log` shows the startup-race timeouts as Warnings
