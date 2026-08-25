## Context

See `proposal.md` for the motivating flake. `renovate-gate.yml`'s every step is scoped with
`if: github.event.pull_request.user.login == 'renovate[bot]'` (a step-level, not job-level, condition — the
job's own header comment explains why: a job-level `if:` would leave the check permanently pending on
non-Renovate PRs if ever marked required).

## Goals / Non-Goals

**Goals:**
- Make a failing Renovate-gate E2E run diagnosable via the same artifact `image.yml` already produces.

**Non-Goals:**
- Not diagnosing the actual `ProvisionedCertificate_IsReusedAfterRestart` root cause here — that is a
  separate, follow-up investigation once real logs exist.

## Decisions

**Mirror `image.yml`'s step exactly, same scoping convention as the rest of this job.** Placed after the
"Full gate" step, `if: failure()` combined implicitly with the job's own Renovate-only steps already having
run (a non-Renovate PR's steps are all skipped, so `artifacts/e2e-logs/` never exists there —
`if-no-files-found: ignore` keeps that silent, no extra condition needed).

## Risks / Trade-offs

None beyond the change itself — this only adds an artifact upload, no behavior change to the gate itself.
