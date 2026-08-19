## Why

`add-e2e-release-gate` correctly gates release publishing on the E2E suite passing, but two independent real
GitHub Actions runs (`workflow_dispatch`, no code differences between them) both failed the E2E suite itself —
not the gate, which worked exactly as designed. Both failures are the same shape: `dockerproxy` starts quickly,
but `dockyarp`/`ca-bundle` never finish. Two genuine local reproduction attempts (cold external-image cache;
CPU/RAM constrained to GitHub's published `ubuntu-latest` spec) both completed the same work in well under two
minutes, ruling out generic resource/cache constraints — so a first fix (raising
`AspireAppHostFixture.cs`'s `StartupTimeoutSeconds` from 180s to 420s, plus uploading `artifacts/e2e-logs/` as
a CI artifact since neither real failure's console output showed anything beyond `dockyarp.log`'s own,
uninformative resource-wait bookkeeping) was shipped as a pragmatic first lever. **It didn't work** — a third
real run still failed, now with the uploaded diagnostics finally showing the real cause: `stepca.log` contains
`/entrypoint.sh: line 59: /home/step/password: Permission denied`. `step-ca`'s own container cannot write into
its bind-mounted `/home/step` directory on a native Linux runner. This is deterministic, not a timing issue —
no timeout increase could ever have fixed it, and it explains why local reproduction on Windows/Docker Desktop
never surfaced it: Docker Desktop's WSL2 bind-mount translation is permissive regardless of the container's
internal UID, masking exactly this class of bug. Until this is fixed, every real release-tag publish fails at
the gate.

## What Changes

- **The actual fix**: `TlsHarness.PrepareClientCa()` makes `E2EPaths.StepCaDirectory` world-writable on Linux
  before `step-ca`'s container mounts it — the exact same `File.SetUnixFileMode(..., worldWritable)` pattern
  already used for `E2EPaths.CertsDirectory` in the same file (`PrepareCertsDirectory()`), for the identical
  reason (a non-root container UID writing into a host-created bind mount).
- The two earlier changes are kept, now correctly understood as secondary, not the fix: `StartupTimeoutSeconds`
  stays at 420s (a reasonable safety margin regardless, doesn't hurt), and the `artifacts/e2e-logs/` CI-artifact
  upload stays — it's literally how this real root cause was found, and remains valuable for any future E2E
  failure on CI, not just this one.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: "End-to-end diagnostics capture" — the per-resource log files the suite already writes are now
  retrievable after a CI run ends (as a workflow artifact), not only from a still-running local session.

## Impact

- `tests/DockYarp.E2E.Tests/TlsHarness.cs` — the actual fix: `E2EPaths.StepCaDirectory` made world-writable on
  Linux, mirroring the existing `PrepareCertsDirectory()` pattern.
- `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs` (`StartupTimeoutSeconds` constant, kept at 420 as a
  reasonable margin, no longer believed to be the actual fix on its own).
- `.github/workflows/image.yml` (`publish-release` job's artifact-upload step, kept — it found this bug).
- **Real CI validation required** (this project's own established practice, and this exact bug was only ever
  visible on a real Linux runner) — a local run cannot confirm or refute a bind-mount-permission fix that is
  invisible on Windows/Docker Desktop by construction; a real `workflow_dispatch` run is the only way to know.
