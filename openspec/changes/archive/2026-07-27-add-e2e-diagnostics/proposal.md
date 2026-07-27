## Why
When an end-to-end run fails, the Aspire containers are torn down immediately, so their logs are gone — and
the Nuke output is light. There is no way to see *why* a resource misbehaved after the fact. A prior attempt
that forwarded logs to the test console was rejected (noisy, and it did not surface in `build.log`). Capture
the logs to **durable files** instead.

## What Changes
- In `AspireAppHostFixture`, register a `ResourceFileLoggerProvider` on the AppHost logging pipeline (where the
  testing host redirects resource logs) that writes each resource's logs to `artifacts/e2e-logs/<resource>.log`
  — host files that survive container teardown. The test console stays silent.
- The Nuke `E2E` target points `DOCKYARP_E2E_LOG_DIR` at `artifacts/e2e-logs` (created before the run) and, on
  failure, logs the directory and a tail of `dockyarp.log`.
- Logs are kept on every run (last run retained under `artifacts/`, which is git-ignored).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the end-to-end suite captures per-resource logs to durable files, and the `E2E` target
  surfaces the diagnostics location on failure.

## Impact
- **Code**: `tests/DockYarp.E2E.Tests/AspireAppHostFixture.cs`, `build/Build.cs`, `docs/deployment.md`.
- **Deferred**: a separate resource-state timeline file — resource state transitions can be added later if the
  per-resource logs prove insufficient.
- **Owning agent**: AG-DEP.
- **Runtime**: exercised under the opt-in `E2E` target (Docker); compile-validated now, behavior seen at the
  next `E2E` run.
