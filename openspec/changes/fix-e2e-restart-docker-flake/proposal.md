## Why

`ProvisionedCertificate_IsReusedAfterRestart` intermittently fails with `Aspire.Hosting.
DistributedApplicationException: Stopped waiting for resource 'dockyarp' to become healthy because it failed
to start` — confirmed on 2 of 6 simultaneously-rebased Renovate PRs on totally unrelated package updates
(gRPC, postcss), ruling out an application-code regression. Root-caused via a real `e2e-logs` diagnostics
artifact (unblocked by the prior `add-renovate-gate-e2e-diagnostics` change): Docker is transiently slow to
stop/delete the old `dockyarp` container on a loaded GitHub Actions runner, and Aspire's own
`WaitForResourceHealthyAsync` fails fast on the resulting failed-to-start state rather than waiting out the
caller's token budget — so the existing `RestartTimeoutSeconds` (180s) was never actually the bottleneck.

## What Changes

- `AspireAppHostFixture.RestartProxyAndWaitHealthyAsync` now retries the restart-command-then-wait-healthy
  sequence up to 3 times (5s delay between attempts) when `WaitForResourceHealthyAsync` throws
  `Aspire.Hosting.DistributedApplicationException`, instead of failing on the first attempt.

## Capabilities

Pure test-infrastructure resilience change — no product-facing behavior changes. `skip_specs: true` is set
in this change's `.openspec.yaml` (no capability deltas).

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs` only.
