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

- [ ] 6.1 Push this fix and trigger another real `workflow_dispatch` run — confirm the permission fix resolves
      the failure outright. This is the only way to know; the bug is by-construction invisible on any local
      Windows/Docker Desktop run (WSL2's permissive bind-mount translation), so no local test can substitute.
- [ ] 6.2 If it still fails: read the (now available) `artifacts/e2e-logs/` artifact again before guessing
      further — don't assume this was the only issue without checking.

## 7. Spec sync prep (AG-DEP)

- [ ] 7.1 Verify the delta spec's MODIFIED "End-to-end diagnostics capture" requirement (the new CI-retrievable
      scenario) matches what actually shipped before archiving.
