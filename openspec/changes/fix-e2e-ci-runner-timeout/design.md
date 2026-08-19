## Context

See `proposal.md` — Why, and `openspec/backlog/items/fix-e2e-ci-runner-timeout.md`'s "Investigation log" section
for the full local-reproduction evidence (two negative attempts) and the two real CI failures — read both
before re-deriving anything here.

Key facts already established, not re-derived:
- `AspireAppHostFixture.cs:33`: `private const int StartupTimeoutSeconds = 180;`.
- Both real failures stall specifically between `dockerproxy` becoming ready and `dockyarp`/`ca-bundle`
  completing — `dockyarp` `.WaitForCompletion(caBundle)` in `Program.cs`, and `caBundle`'s own script polls
  `until [ -s .../intermediate_ca.crt ] && [ -s .../root_ca.crt ]; do sleep 1; done` for `stepca`'s PKI files.
  Neither real failure's console output includes `stepca.log`/`ca-bundle.log` — `Build.cs`'s `E2E` target only
  tails `dockyarp.log` to console on failure, and nothing uploads the full `artifacts/e2e-logs/` directory
  before the runner is torn down.
- Local reproduction (cold cache, matched CPU/RAM, both combined) all completed the same startup+full-suite
  work in under two minutes — the underlying work is not fundamentally broken.

**Correction, found live, after this design's original decisions were already implemented and validated once
(not assumed — read before trusting the "Decisions" section below at face value)**: the `StartupTimeoutSeconds`
bump + artifact upload were pushed and validated via a real `workflow_dispatch` run. The timeout bump alone did
**not** fix it — a third real run still failed, stalling at ~407s of the new 420s budget, the same shape again.
But the artifact upload (which *did* work) finally produced `stepca.log`, and it contains the real answer:
```
/entrypoint.sh: line 59: /home/step/password: Permission denied
```
`step-ca`'s own container cannot write into its bind-mounted `/home/step` (`E2EPaths.StepCaDirectory`) on a
native Linux runner — deterministic, not timing-related, and invisible on Windows/Docker Desktop because its
WSL2 bind-mount translation is permissive regardless of the container's internal UID. This is the same class
of bug `TlsHarness.PrepareCertsDirectory()` already fixes for `E2EPaths.CertsDirectory`
(`File.SetUnixFileMode(..., worldWritable)`, guarded `!OperatingSystem.IsWindows()`) — just never applied to
`StepCaDirectory`. The "Decisions" section below is left as originally written (both decisions were reasonable
given the evidence available at the time, and neither is wrong to keep); a new Decision follows for the actual
fix.

## Goals / Non-Goals

**Goals:**
- Give the release-gate E2E run enough real wall-clock headroom that it isn't racing a budget local evidence
  suggests is simply too tight for GitHub's actual infra, whatever the precise cause.
- If the timeout bump alone isn't sufficient, the *next* failure (if there is one) finally shows which specific
  resource (`stepca` vs `ca-bundle` vs something else) is actually slow — not just that the fixture's own
  deadline expired, which both real failures already told us and doesn't move the investigation forward again.

**Non-Goals:**
- Definitively root-causing *why* GitHub's infra is slower (network path to Docker Hub, per-core compute,
  disk I/O) — genuinely not answerable without either the log-upload's new data or GitHub-side telemetry this
  project has no access to. This change makes the next data point available; it doesn't itself explain it.
- Making the timeout configurable per-environment (e.g. via an env var) — the current single hardcoded constant
  is fine; introducing configurability here would be scope beyond what's needed to fix the actual problem.
- Reducing the container count / backend catalog for the release-gate run specifically (the item stub's option
  3) — not pursued here; the timeout bump is the cheaper first lever, and shrinking the catalog would reduce
  what the release gate actually validates, a bigger trade-off to make without first knowing whether it's even
  necessary.

## Decisions

**Raise `StartupTimeoutSeconds` to 420 (7 minutes) — a generous, round, deliberately-not-precisely-derived
margin, not a scientifically measured minimum.**

Rationale: neither real failure came close to finishing organically before being cancelled at ~180s (both
stopped right at the deadline, not earlier) — there's no data suggesting the real completion time, only that
180s isn't enough. Local runs complete the equivalent work in under 2 minutes reliably; even a generous
3–4× multiplier for genuinely slower CI infra lands well under 7 minutes. If 420s still isn't enough, that
itself is useful information (points away from "just needs more time" and toward a real hang), which the
log-upload (below) will help distinguish from a resource that's merely slow.

**Upload `artifacts/e2e-logs/` as a GitHub Actions workflow artifact on the `publish-release` job's E2E
failure, rather than only printing `dockyarp.log`'s tail to console (already done).**

Rationale: this is the actual diagnostic lever for *if* the timeout bump isn't sufficient. Both real failures
so far only showed `dockyarp.log`'s own content — which, because `dockyarp` itself never started, is just
Aspire's own resource-wait bookkeeping, not `stepca`'s or `ca-bundle`'s actual logs. Without those, a third
failure would tell us nothing new. `actions/upload-artifact@v7` (checked against the action's own latest
release before pinning, matching this project's established practice of verifying third-party action versions
rather than guessing) with `if: failure()` scoped to this one step keeps it from affecting successful runs at
all.

**Make `E2EPaths.StepCaDirectory` world-writable on Linux in `TlsHarness.PrepareClientCa()`, mirroring
`PrepareCertsDirectory()`'s existing pattern for `CertsDirectory`.**

Rationale: this is the actual fix — a bind-mount-permission mismatch between the host-created directory and
`step-ca`'s non-root container UID, exactly the class of bug this project already has a working, precedented
fix for elsewhere in the same file. No new pattern invented; reused verbatim (`UnixFileMode` combining
user/group/other read+write+execute, `File.SetUnixFileMode`, guarded `!OperatingSystem.IsWindows()` since
Windows/Docker Desktop's bind-mount translation doesn't need it and `SetUnixFileMode` isn't supported there
anyway). Considered and rejected: fixing this inside the `smallstep/step-ca` image or its entrypoint instead —
not this project's image to change; fixing host-side permissions is the correct, minimal-footprint side to own
the mitigation on, same as the existing `CertsDirectory` precedent already does for DockYarp's own container.

## Risks / Trade-offs

- [Risk] 420s might still not be enough for some *other* reason once the permission bug is fixed (unlikely,
  but the timeout bump was validated live to be at least not obviously wrong, just not the actual fix). →
  Mitigation: still kept as a reasonable margin; a fourth real run (task 4 in `tasks.md`) will confirm the
  permission fix resolves it outright, with the artifact-upload lever still in place if anything unexpected
  remains.
- [Risk] A 7-minute startup budget is a long time to wait before a release fails, if it ever does hang for a
  genuinely different reason later. → Accepted: correctness (not silently timing out on legitimately slow but
  working infra) matters more here than shaving CI minutes off a failure path that should be rare.
- [Risk] Other bind-mounted directories in the E2E harness could have the same latent bug, just not yet hit
  (e.g. `ClientCaDirectory` is mounted read-only so this specific failure mode doesn't apply there, but this
  wasn't exhaustively audited for every mount). → Not fixed pre-emptively here (scope discipline — fix the
  confirmed bug, don't speculatively patch unconfirmed ones); worth a quick audit as a follow-up note if
  another CI-only E2E failure surfaces later with a similar shape.
