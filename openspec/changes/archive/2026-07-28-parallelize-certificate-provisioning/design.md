## Context
`ReconcileAsync` (`src/DockYarp.Tls/CertificateProvisioningService.cs:37-59`) iterates
`TlsDomains.Desired(snapshot)` sequentially, `await`ing each `RequestCertificateAsync` — which awaits
`WaitForValidationAsync` (`src/DockYarp.Tls/CertesAcmeClient.cs:74-92`, up to `30 × 2 s = 60 s`). Per-host
failures are already isolated (try/catch, `CA1031` suppressed with justification), but the sequential `await`
causes **head-of-line blocking**. `FileCertificateStore` is thread-safe (a `Lock` guards `Find`/`Save`/`List`;
`Save` writes a per-host `.pfx`), and `store.Current` returns an immutable snapshot.

## Goals / Non-Goals
- **Goal**: one slow/failing host must not delay/block provisioning of the others.
- **Non-Goal**: unbounded concurrency (ACME rate limits); making the degree/timeout configurable (later).

## Decisions
- Replace the `foreach` with `Parallel.ForEachAsync` over `TlsDomains.Desired(snapshot)`, with
  `MaxDegreeOfParallelism` = a small bounded const, and the `CancellationToken` flowed. Keep the per-host
  try/catch; **rethrow `OperationCanceledException`** so cancellation propagates and stops the pass promptly.
- Skip hosts that do not need a certificate inside the loop body (`NeedsCertificate`).

## Risks / Trade-offs
- Concurrent ACME orders on one account are allowed; the bounded degree limits load and avoids rate limits.
- Thread-safety: the production store is safe; the test fake store must be made thread-safe.

## Migration Plan
- None (behavioral only: provisioning is faster and resilient; per-host results unchanged).

## Open Questions
- Whether to make the degree of parallelism / validation timeout configurable (deferred).
