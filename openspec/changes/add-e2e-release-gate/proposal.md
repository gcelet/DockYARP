## Why

`image.yml`'s release path (a `v*` tag push) runs `DockerPublish` directly with no test gate at all — a broken
build can be published today. The Nuke `Release` target already wires `Test`, `E2E`, and `DockerImage` together
locally, but CI never calls it before publishing. The end-to-end suite is the only thing that exercises real
TLS/ACME/mTLS handshakes and Docker discovery; skipping it on the one path that actually ships to users defeats
its purpose.

## What Changes

- `build/Build.cs`'s `DockerPublish` target now `.DependsOn(Test, E2E)` directly on itself — matching the
  precedent `DockerImage.DependsOn(Test)` already sets. **This is not the same as listing targets side by side
  on a CI command line**: an earlier draft of this change tried `./build.sh Test E2E DockerPublish ...` and,
  verified live, found that gives *no* real ordering/failure guarantee between unrelated targets — only a
  target's *own* `.DependsOn()` is guaranteed to run (and succeed) before that target's body starts. A new
  `[Parameter] SkipPublishGate` (default `false`) lets a quick manual/local push opt out explicitly; CI never
  sets it, so a release publish is gated by default with no CI-side orchestration needed at all.
- `publish-edge` (trunk pushes, no tag) passes `--skip-publish-gate` explicitly — edge exists for fast
  in-development signal, and per-push E2E gating there would slow every trunk commit for a channel that isn't a
  release. `base-image-refresh.yml`'s `:latest` republish is left gated (no flag) — infrequent enough that the
  extra time doesn't matter, and it still ships to real users.
- **Decision, not deferred**: `add-nondcp-e2e-harness` is **not** a prerequisite. That item exists to unblock
  two specific scenarios (`e2e-host-network-mode`, `e2e-multi-network`) that DCP cannot support at all — an
  unrelated, orthogonal concern to whether the *existing* Aspire/DCP e2e suite runs reliably on a GitHub-hosted
  runner. Whether DCP is reliable enough on CI is genuinely unverified; this change finds out with a real run
  rather than assuming a dependency that doesn't actually block it.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: "Continuous image publishing" — a release-tag publish now requires the `Test` and `E2E` gates
  to pass first; a failing gate blocks the image from being pushed.

## Impact

- `build/Build.cs`: `DockerPublish` target gains `.DependsOn(Test, E2E)`; new `[Parameter] SkipPublishGate`.
  This changes `DockerPublish`'s behavior for **every** caller (CI and local/manual), not just the CI release
  path — a real, deliberate change beyond the CI-only scope originally assumed, corrected mid-implementation
  once the CLI-list approach was found not to actually gate anything.
- `.github/workflows/image.yml`: `publish-release`'s step is otherwise unchanged (`./build.sh DockerPublish
  ...` — gating is now intrinsic, no CI-side orchestration needed); `publish-edge`'s step gains
  `--skip-publish-gate`.
- `.github/workflows/base-image-refresh.yml`: no invocation change, one clarifying comment (its `:latest`
  republish is now gated too, implicitly, since it never passes `--skip-publish-gate`).
- No application code changes (`src/`/`tests/` untouched) — only the build tooling and CI workflows.
