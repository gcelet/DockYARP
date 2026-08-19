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

## 4. Real CI validation — required (AG-DEP)

- [ ] 4.1 Push this change and trigger a real `workflow_dispatch` run on `image.yml` (`publish-release` job) —
      this is the only way to know if the fix worked; local runs cannot confirm or refute a CI-runner-specific
      timing issue. Record the outcome plainly: passed cleanly, or failed again with the new artifact now
      showing which resource is actually slow.
- [ ] 4.2 If it fails again: download and read the uploaded `artifacts/e2e-logs/` artifact (`stepca.log`,
      `ca-bundle.log`, `dockerproxy.log`) before deciding on a next step — don't guess further without looking
      at the new data this change exists to produce.

## 5. Spec sync prep (AG-DEP)

- [ ] 5.1 Verify the delta spec's MODIFIED "End-to-end diagnostics capture" requirement (the new CI-retrievable
      scenario) matches what actually shipped before archiving.
