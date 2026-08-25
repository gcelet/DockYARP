## Why

7 Renovate PRs are currently failing identically on `ProvisionedCertificate_IsReusedAfterRestart` (Aspire
"failed to become healthy" after restart), does not reproduce locally, and `renovate-gate.yml` has no way to
recover the real per-resource container logs from a failing run — only the pre-restart console tail is
visible. Without this, the actual root cause cannot be diagnosed, only guessed at.

## What Changes

- Add an "Upload E2E diagnostics" step to `renovate-gate.yml`'s `full-gate` job, mirroring the step
  `image.yml`'s two jobs already have.

## Capabilities

Pure CI-tooling change — no product-facing behavior changes. `skip_specs: true` is set in this change's
`.openspec.yaml` (no capability deltas).

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `.github/workflows/renovate-gate.yml` only.
