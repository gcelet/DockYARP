## 1. Give the fixture more real headroom (AG-DEP)

- [x] 1.1 `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs`: raise `StartupTimeoutSeconds` from `180` to `420`,
      with a comment explaining why (generous margin, not a measured minimum — see design.md).

## 2. Make the diagnostics actually retrievable from CI (AG-DEP)

- [x] 2.1 `.github/workflows/image.yml`: add an `actions/upload-artifact@v7` step to the `publish-release` job,
      `if: failure()`, uploading `artifacts/e2e-logs/` (the directory `Build.cs`'s `E2E` target already writes
      per-resource logs into) — scoped so it only triggers when the "Test, E2E, and push (Nuke)" step fails,
      not on every run. `if-no-files-found: ignore` so the upload step itself doesn't error when the directory
      was never created (e.g. `Test` failed before `E2E` ever ran).
- [x] 2.2 Confirmed: `Build.cs:79-80` — `ArtifactsDirectory => RootDirectory / "artifacts"`,
      `E2ELogDirectory => ArtifactsDirectory / "e2e-logs"` — matches the real failure logs'
      `/home/runner/work/DockYARP/DockYARP/artifacts/e2e-logs`, i.e. `artifacts/e2e-logs/` relative to the
      workflow's default working directory (the repo root).

## 3. Local sanity check (AG-DEP)

- [x] 3.1 `dotnet build tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` — 0 warnings, 0 errors. Compile check
      only, not a validation — the bug is CI-runner-specific, confirmed via extensive local reproduction
      attempts in the backlog item's investigation log, so this cannot confirm the actual fix.
- [x] 3.2 `actionlint` not available locally; validated `image.yml` parses as syntactically correct YAML via
      `npx js-yaml` (no actionlint-specific schema check, but rules out a syntax/indentation mistake).

## 4. Real CI validation, round 1 — required (AG-DEP)

- [x] 4.1 Pushed and triggered a real `workflow_dispatch` run on `image.yml` (`publish-release` job) —
      https://github.com/gcelet/DockYARP/actions/runs/32293946454. Result: **failed again** — the timeout bump
      alone was not sufficient (stalled at ~407s of the new 420s budget, same shape as before).
- [x] 4.2 Downloaded and read the uploaded `artifacts/e2e-logs/` artifact (`gh run download ... -n e2e-logs`).
      **Found the real root cause**: `stepca.log` contains `/entrypoint.sh: line 59: /home/step/password:
      Permission denied` — a bind-mount permission issue, not a timing issue. See design.md's Context
      correction and proposal.md's updated Why for the full finding.

## 5. The actual fix (AG-DEP)

- [x] 5.1 `tests/DockYarp.E2E.Tests/TlsHarness.cs`: `PrepareClientCa()` makes `E2EPaths.StepCaDirectory`
      world-writable on Linux (`File.SetUnixFileMode`, guarded `!OperatingSystem.IsWindows()`), mirroring the
      existing `PrepareCertsDirectory()` pattern for `CertsDirectory` verbatim.
- [x] 5.2 `dotnet build tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` — 0 warnings, 0 errors.

## 6. Real CI validation, round 2 — required (AG-DEP)

- [x] 6.1 Pushed the `StepCaDirectory` write-permission fix and triggered another real `workflow_dispatch` run —
      https://github.com/gcelet/DockYARP/actions/runs/32296322764. Result: **massive improvement, not full
      resolution** — 31/32 tests passed (up from complete E2E startup failure), confirming the write-permission
      fix works. One new, different failure: `AcmeCertificate_ChainIncludesIntermediate` —
      `OpenSslCryptographicException` (`error:10080002:BIO routines::system lib`) loading
      `certs/root_ca.crt` via `X509CertificateLoader.LoadCertificateFromFile`.
- [x] 6.2 Diagnosed without needing another artifact download: the failing test reads `root_ca.crt` directly
      from the host filesystem, while its passing sibling (`MountedPemCertificate_ChainIncludesIntermediate`)
      uses an in-memory certificate and never touches disk — isolating the failure to reading a file step-ca's
      own container created. Root cause: `PrepareClientCa()`'s chmod on `StepCaDirectory` runs once, before
      step-ca ever starts, so it cannot reach `certs/root_ca.crt` — a file step-ca creates *later*, under its
      own container's umask.

## 7. The read-side fix, attempt 1 — host-side recursive chmod (AG-DEP)

- [x] 7.1 `tests/DockYarp.E2E.Tests/TlsHarness.cs`: new `MakeStepCaPkiReadable()` recursively widens permissions
      on everything under `StepCaDirectory` at call time (`Directory.EnumerateFileSystemEntries(...,
      SearchOption.AllDirectories)` + `File.SetUnixFileMode`), guarded `!OperatingSystem.IsWindows()`.
- [x] 7.2 `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs`: call it in `StartAsync()` right after
      `WaitForResourceHealthyAsync(ProxyResource, token)`.
- [x] 7.3 `dotnet build tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` — 0 warnings, 0 errors.
- [x] 7.4 Real CI run (`32297816857`) — **failed at `OneTimeSetUp` entirely**, worse than before:
      `UnauthorizedAccessException`/`IOException: Operation not permitted` inside `File.SetUnixFileMode` itself.
      Root cause: `chmod` requires being the file's *owner* (or root) — the host `runner` process is neither for
      files step-ca's own container UID created. This approach is structurally broken, not just incomplete;
      reverted (7.1/7.2 removed from the codebase, kept here for the record).

## 8. The read-side fix, attempt 2 — chmod from the container that already owns write access (AG-DEP)

- [x] 8.1 `tests/DockYarp.E2E.AppHost/Program.cs`: `ca-bundle` (alpine, root by default, no `WithUser`
      override) already bind-mounts `StepCaDirectory` read-write and already successfully reads
      `root_ca.crt`/`intermediate_ca.crt` from it — the natural place to fix read permissions, since root bypasses
      ownership checks for `chmod`. Appended `chmod -R a+rX /stepca` to `bundleScript`, right after writing
      `ca-bundle.crt`. `a+rX` only *adds* bits (read for all, execute for dirs/already-executable files); cannot
      strip step-ca's own write access to files it creates later.
- [x] 8.2 `dotnet build` (AppHost + Tests) — 0 warnings, 0 errors.
- [x] 8.3 Local sanity run (`./build.ps1 E2E`) — 32/32 passed, 1:26 total. Sanity only, not a validation: this
      bug class is by construction invisible on Windows/Docker Desktop.

## 9. Real CI validation, round 4 — required (AG-DEP)

- [x] 9.1 Pushed (commit `91fec88`) and triggered another real `workflow_dispatch` run —
      https://github.com/gcelet/DockYARP/actions/runs/32298840624. **Fully green**: `Passed! - Failed: 0,
      Passed: 32, Skipped: 0, Total: 32` for `DockYarp.E2E.Tests.dll`, all other test projects also 100% green
      (`Docker`, `IntegrationTests`, `Tls`, `Core`, `Security`). The job's 12m36s wall time is normal multi-arch
      `DockerPublish` push time under QEMU emulation (~6.5 min of it), not a stall — the E2E test run itself
      took 1m18s.
- [x] 9.2 Not needed — no failure to diagnose.

## 10. Spec sync prep (AG-DEP)

- [x] 10.1 Verified: the delta spec's MODIFIED "End-to-end diagnostics capture" requirement (CI-retrievable
      `artifacts/e2e-logs/` upload on failure) matches what shipped — `actions/upload-artifact@v7`,
      `if: failure()`, `path: artifacts/e2e-logs/`, `if-no-files-found: ignore` in `.github/workflows/image.yml`.
