## Context

See `proposal.md` for the root-caused diagnosis, backed by a real `Aspire.Hosting.Dcp.DcpExecutor.log` from a
failing CI run: multiple "still running, trying again to stop it..." retries, two delete attempts still
reporting the resource present, a third finally returning `NotFound`, and a 5s log-flush timeout — all before
Aspire's `WaitForResourceHealthyAsync` gave up. The test method already carries `[Retry(2)]` (added the prior
evening for the unrelated ACME-timeout flake).

## Goals / Non-Goals

**Goals:**
- Retry precisely the flaky operation (the restart-command-then-wait-healthy sequence), not the whole test —
  the test's pre-restart wait (`ServedAcmeThumbprintAsync`, up to 150s) does not need repeating.
- Give Docker's earlier stop/delete cleanup real time to finish before the next attempt.

**Non-Goals:**
- Not investigating why the test-level `[Retry(2)]` did not visibly retry the restart operation — real e2e-logs
  show only one attempt across the whole run, but this fix does not depend on that mechanism working, so it is
  left as a separate, still-open curiosity rather than blocking this change.
- Not increasing `RestartTimeoutSeconds` — confirmed via the real logs that the failure is a fail-fast on an
  observed bad state, not a timeout being exhausted, so a bigger budget would not have helped.

## Decisions

**Retry inside `RestartProxyAndWaitHealthyAsync`, not only via the outer NUnit `[Retry]`.** This targets the
confirmed root cause directly and deterministically, regardless of whatever caused the NUnit-level retry not
to produce a second observable attempt. `[Retry(2)]` stays on the test method as a cheap outer safety net —
it costs nothing when the inner retry already succeeds.

**Catch `Aspire.Hosting.DistributedApplicationException` specifically, not a broad `catch`.** Confirmed via
`dotnet-inspect` against the real `Aspire.Hosting` package that this is the actual exception type
`WaitForResourceHealthyAsync` throws on a failed-to-start resource — narrowing the catch avoids accidentally
swallowing an unrelated failure (e.g. a genuine `OperationCanceledException` from the caller's own token).

**3 attempts, 5s delay**: matches the real log's own observed 5s log-flush timeout as a reasonable interval
for Docker's cleanup to actually settle before retrying; 3 total attempts bounds the worst case to roughly
the same order of magnitude as the original single-attempt budget, without being unbounded.

## Risks / Trade-offs

- [Risk] If the underlying Docker slowness is a persistent condition for the whole CI run (not a one-off
  blip), all 3 attempts could still fail → Mitigation: the exception still propagates after exhausting
  attempts — no silent masking, and `[Retry(2)]` at the test level remains as a further outer safety net.
- [Risk] Retrying could mask a genuine future regression in the restart/health flow → Mitigation: bounded (3
  attempts, not unbounded), scoped to exactly the one exception type confirmed to be the real failure mode.
