## Why

`add-e2e-release-gate` correctly gates release publishing on the E2E suite passing, but two independent real
GitHub Actions runs (`workflow_dispatch`, no code differences between them) both failed the E2E suite itself —
not the gate, which worked exactly as designed. Both failures are the same shape: `dockerproxy` starts quickly,
but `dockyarp`/`ca-bundle` never finish within `AspireAppHostFixture.cs`'s 180-second `StartupTimeoutSeconds`.
Two genuine local reproduction attempts (cold external-image cache; CPU/RAM constrained to GitHub's published
`ubuntu-latest` spec, 2 vCPU/~6.8 GB, via a temporary `.wslconfig`) both completed the same work in well under
two minutes — ruling out generic resource/cache constraints as sufficient explanation on their own. Until this
is fixed, every real release-tag publish fails at the gate.

## What Changes

- `AspireAppHostFixture.cs`'s `StartupTimeoutSeconds` raised with a generous margin — local evidence shows the
  underlying work isn't broken (completes reliably under two minutes even under matched constraints), so the
  most likely explanation is real GitHub-infra-specific slowness (registry pull path, per-core compute) rather
  than a genuine hang; more wall-clock budget is the pragmatic first lever.
- `image.yml`'s `publish-release` job uploads `artifacts/e2e-logs/` as a workflow artifact on failure — today
  those per-resource logs (`stepca.log`, `ca-bundle.log`, `dockerproxy.log`) are written to a file specifically
  so a failure can be diagnosed, but a GitHub Actions runner is torn down after the job ends, so they were
  never actually retrievable from either real failure. This is the actual diagnostic lever if the timeout bump
  alone doesn't resolve it — a confirming run will finally show which specific resource is slow, not just that
  the fixture's own deadline expired.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: "End-to-end diagnostics capture" — the per-resource log files the suite already writes are now
  retrievable after a CI run ends (as a workflow artifact), not only from a still-running local session.

## Impact

- `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs` (`StartupTimeoutSeconds` constant only).
- `.github/workflows/image.yml` (`publish-release` job gains an artifact-upload step, conditional on the E2E
  step's failure).
- **Real CI validation required** (this project's own established practice, and this exact bug was only ever
  visible on a real runner) — a local `dotnet test`/`./build.ps1 E2E` run cannot confirm or refute this fix; a
  real `workflow_dispatch` run is the only way to know if it worked, and the user has explicitly signed off on
  running it despite generally preferring to avoid GitHub Actions runs for exploratory checks.
