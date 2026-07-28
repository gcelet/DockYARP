## 1. Concurrency (AG-AT)
- [x] 1.1 Replace the sequential `ReconcileAsync` loop with `Parallel.ForEachAsync` (bounded
      `MaxDegreeOfParallelism`), keeping the per-host try/catch isolation and flowing the `CancellationToken`;
      rethrow `OperationCanceledException`

## 2. Tests (AG-AT)
- [x] 2.1 Make the test `FakeCertificateStore` thread-safe (`ConcurrentDictionary`)
- [x] 2.2 Unit test (`ProvisionsHostsConcurrently`): two hosts whose ACME requests each wait for the other to
      start are **both** provisioned — only possible under concurrent (not sequential) provisioning
