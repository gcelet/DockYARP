## 1. Wire the gate into Build.cs (AG-DEP)

- [x] 1.1 `build/Build.cs`: new `[Parameter("DockerPublish: skip the Test/E2E gate before pushing — for a
      quick manual/local push only; CI never sets this")] readonly bool SkipPublishGate;`, placed alongside the
      other `DockerPublish`-related parameters (`PublishTag`, `Edge`).
- [x] 1.2 `build/Build.cs`: `Target DockerPublish` gains `.DependsOn(SkipPublishGate ? [] : [Test, E2E])` on
      itself (alongside its existing `.DependsOn(GenerateVersionDetails)`) — matching the established
      `DockerImage.DependsOn(Test)` precedent. **Correction from the original plan**: an earlier attempt used
      `./build.sh Test E2E DockerPublish ...` (three unrelated top-level targets on one CI command line)
      instead — verified live that this gives no real ordering/failure guarantee at all (see design.md's
      Context/Decisions). Only a target's own `.DependsOn()` is a real gate.
- [x] 1.3 `.github/workflows/image.yml`: `publish-release`'s step stays `./build.sh DockerPublish ...`
      unchanged (gating is now intrinsic to the target) — comment updated to explain why no CI-side
      orchestration is needed.
- [x] 1.4 `.github/workflows/image.yml`: `publish-edge`'s step gains `--skip-publish-gate` (edge stays fast,
      see design.md's Decisions) — comment added explaining why.
- [x] 1.5 `.github/workflows/base-image-refresh.yml`: no invocation change (its `:latest` republish is gated by
      default, since it never passes `--skip-publish-gate`); one clarifying comment added.

## 2. Verify locally before trusting CI (AG-DEP)

- [x] 2.1 Confirmed live end to end: a local throwaway registry (`registry:2` on `localhost:5999`) + `./build.ps1
      DockerPublish --registry localhost:5999 --image-repository dockyarp --version 0.0.0-gate-test --platforms
      linux/amd64` ran `Test` (0:09) then `E2E` (1:12) then `DockerPublish` (0:23) and the image landed on the
      registry (`GET /v2/dockyarp/tags/list` → `["0.0.0-gate-test"]`) — the happy path genuinely publishes.
- [x] 2.2 Confirmed live: a deliberately-failing test (a temporary throwaway test file, removed immediately
      after) made `Test` fail and `DockerPublish` show `NotRun` in the Nuke target summary — the *real*
      dependency-graph gate, not the earlier CLI-list false positive (which also showed `NotRun` for the wrong
      reason — see design.md's Context for that comparison).

## 3. Real CI validation — required (AG-DEP)

- [ ] 3.1 Push this change and observe a real `publish-release` run on GitHub Actions (either via a real
      release-shaped ref or `workflow_dispatch`) — confirm `Test` and `E2E` actually run on the hosted runner
      and the job succeeds end to end, including the actual image push. This is the real answer to whether
      Aspire/DCP holds up on a GitHub-hosted runner (design.md's flagged risk) — record the outcome.
- [ ] 3.2 If the real run reveals Aspire/DCP flakiness on the runner, do not silently retry-and-ignore — capture
      the failure mode as a note (new backlog item candidate if it's a real, recurring problem) rather than
      quietly working around it in this change.

## 4. Spec sync prep (AG-DEP)

- [ ] 4.1 Verify the delta spec's MODIFIED "Continuous image publishing" requirement (the new gate + trunk-push
      exemption scenarios) matches what actually shipped in sections 1–3 before archiving.
