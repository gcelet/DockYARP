## 1. Add restart resilience (AG-DEP)

- [x] 1.1 Wrap the restart-command-then-wait-healthy sequence in `AspireAppHostFixture.
      RestartProxyAndWaitHealthyAsync` in a bounded retry loop (3 attempts, 5s delay), catching
      `Aspire.Hosting.DistributedApplicationException`, and verify `dotnet build
      tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` succeeds with 0 errors.
- [x] 1.2 Confirm the exact exception type/namespace against the real `Aspire.Hosting` package via
      `dotnet-inspect` before using it in a catch clause (not assumed from the diagnostic message).

## 2. Verify (AG-DEP)

- [x] 2.1 Run the full E2E suite (`./build.ps1 E2E`) and verify all 42 tests still pass on a healthy local
      run (first-attempt success path, no regression).
