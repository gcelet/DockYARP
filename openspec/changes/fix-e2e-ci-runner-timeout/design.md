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

## Risks / Trade-offs

- [Risk] 420s might still not be enough, or the real issue is a genuine hang (not merely slow) that no
  timeout increase fixes. → Mitigation: the log-upload means a third failure, if it happens, finally shows
  *which* resource and *why* (e.g. `stepca`'s own container logs, not just Aspire's wait bookkeeping) — real
  progress either way, not another dead end.
- [Risk] A 7-minute startup budget is a long time to wait before a release fails, if it ever does hang for a
  genuinely different reason later. → Accepted: correctness (not silently timing out on legitimately slow but
  working infra) matters more here than shaving CI minutes off a failure path that should be rare.
