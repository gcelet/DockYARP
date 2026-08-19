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

## 7. The read-side fix (AG-DEP)

- [x] 7.1 `tests/DockYarp.E2E.Tests/TlsHarness.cs`: new `MakeStepCaPkiReadable()` recursively widens permissions
      on everything under `StepCaDirectory` at call time (`Directory.EnumerateFileSystemEntries(...,
      SearchOption.AllDirectories)` + `File.SetUnixFileMode`), guarded `!OperatingSystem.IsWindows()`.
- [x] 7.2 `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs`: call it in `StartAsync()` right after
      `WaitForResourceHealthyAsync(ProxyResource, token)` — safe only there, since DockYarp
      `.WaitForCompletion(caBundle)` and `ca-bundle` itself polls until step-ca's PKI files exist, so the proxy
      resource reporting healthy transitively guarantees step-ca has finished writing them.
- [x] 7.3 `dotnet build tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` — 0 warnings, 0 errors.

## 8. Real CI validation, round 3 — required (AG-DEP)

- [ ] 8.1 Push this fix and trigger another real `workflow_dispatch` run — confirm the suite is fully green
      (32/32), not just improved again. Same rationale as before: this class of bug is by-construction invisible
      on any local Windows/Docker Desktop run.
- [ ] 8.2 If it still fails: read the `artifacts/e2e-logs/` artifact before guessing further.

## 9. Spec sync prep (AG-DEP)

- [ ] 9.1 Verify the delta spec's MODIFIED "End-to-end diagnostics capture" requirement (the new CI-retrievable
      scenario) matches what actually shipped before archiving.
