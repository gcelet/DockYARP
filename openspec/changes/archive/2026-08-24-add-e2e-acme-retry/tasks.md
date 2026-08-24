## 1. Add scoped retry to the 3 ACME HTTP-01 E2E tests (AG-AT)

- [x] 1.1 Add `[Retry(2)]` to `AcmeCertificate_IsProvisionedForHost` and `AcmeCertificate_ChainIncludesIntermediate`
      in `tests/DockYarp.E2E.Tests/TlsTests.cs`, and verify the file still compiles
      (`dotnet build DockYarp.slnx`).
- [x] 1.2 Add `[Retry(2)]` to `ProvisionedCertificate_IsReusedAfterRestart` in
      `tests/DockYarp.E2E.Tests/RestartPersistenceTests.cs`, and verify the file still compiles.
- [x] 1.3 Confirm no other E2E test was touched — `git diff --stat` shows only the two files above.

## 2. Verify locally (AG-AT)

- [x] 2.1 Run the full E2E suite (`./build.ps1 E2E` or `./build.sh E2E`) and verify all 42 tests still pass,
      confirming `[Retry]` does not change behavior on a healthy run.
- [x] 2.2 Confirm `docs/testing.md`'s e2e coverage map needs no update (no test added or removed) — read it
      and verify no stale reference is introduced.
