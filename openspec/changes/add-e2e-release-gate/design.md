## Context

See `proposal.md` — Why. Current state, confirmed by direct inspection (not assumed):
- `build/Build.cs`'s `Target Release` already `.DependsOn(Test, E2E, DockerImage)` — the local gate exists.
- `.github/workflows/image.yml`'s `publish-release` job calls `./build.sh DockerPublish ...` directly — no
  `Test`/`E2E` step anywhere in that job.
- `ci.yml` explicitly excludes the end-to-end suite ("intentionally not run here") — it only ever runs on
  `./build.sh Test`, never as part of any workflow today.
- `DockerImage.DependsOn(Test)` — an existing target already gates itself directly on `Test`; this is the
  established, working precedent this change follows for `DockerPublish`.
- **Correction made mid-implementation, verified live, not assumed**: invoking `./build.sh Test E2E
  DockerPublish ...` (three unrelated top-level targets on one CLI line) does **not** create any real
  dependency between them. A deliberately-failing test run showed `E2E` completing successfully (1:18) while
  `Test` failed — Nuke did not even respect the listed order between two targets with no relationship — and
  nothing in the graph would have stopped `DockerPublish` from starting regardless, since it has no
  `.DependsOn()` edge to either. `DockerPublish` only showed `NotRun` because Nuke aborts the *whole* CLI
  invocation when *any* requested target fails — not because it was gated. Nuke's own `DependsOn` XML doc is
  explicit that this guarantee ("targets... executed before this target") applies to a target's *own* declared
  dependencies, not to unrelated targets merely requested together.

## Goals / Non-Goals

**Goals:**
- A release-tag (`v*`) publish cannot push an image when `Test` or the end-to-end suite fails.
- No duplicated build/test/publish logic in YAML — the workflow only orchestrates, per the project's own
  guardrail (`AGENTS.md`: "the Nuke build is the single source of truth").

**Non-Goals:**
- Gating `publish-edge` (trunk pushes) the same way — out of scope, see proposal's "What Changes".
- A nightly scheduled E2E run for early signal, independent of releases — the item stub calls this out as
  optional; not built here, left as a natural follow-up if wanted later.
- Building `add-nondcp-e2e-harness` first — see the Decision below.

## Decisions

**`DockerPublish` itself `.DependsOn(Test, E2E)`, gated off by a new `[Parameter] SkipPublishGate` (default
`false`) — not a CI-side multi-target invocation, not a new wrapper target.**

Rationale: only a target's own declared dependencies are actually guaranteed to run (and succeed) before its
body starts — confirmed both by Nuke's own `DependsOn` XML doc and by the live failed experiment in Context
above. A new wrapper target (e.g. `Target ReleasePublish => _ => _.DependsOn(Test, E2E, DockerPublish)`) would
have the *exact same flaw*: `DockerPublish` would still be a bare co-dependency with no ordering/failure edge
to `Test`/`E2E`, since Nuke's guarantee is about the *wrapper's* body waiting for its dependencies, not about
ordering *between* those dependencies. The only mechanism that actually works is the one `DockerImage` already
demonstrates: the consequential target itself depends directly on the gate. Calling the existing local `Release`
target instead was also considered and rejected — it would additionally trigger `DockerImage`, a local
single-platform `--load` build that's pure waste when the actual goal is `DockerPublish`'s own multi-platform
push.

Making `DockerPublish` depend on `Test`/`E2E` unconditionally would also gate every local/manual invocation
(e.g. a quick push to a private registry to test an in-progress fix, done earlier this session) — not scoped to
just CI. `SkipPublishGate` (`--skip-publish-gate`, default `false`) preserves that flexibility explicitly: CI
never passes it, so release publishes stay gated by default; a local developer who deliberately wants a fast
throwaway push can opt out. This directly implements the two mechanisms suggested when this correction was
raised: conditional dependencies driven by a parameter, applied to the one target that actually needs the gate
(rather than a separate always-run "meta task", which — per the paragraph above — would not have been
sufficient on its own without this same direct-dependency fix underneath it).

**`add-nondcp-e2e-harness` is not a prerequisite — the item stub's "DCP timing flakiness" concern does not
apply to this change.**

`add-nondcp-e2e-harness` exists to run containers *outside* DCP for two scenarios DCP architecturally cannot
support (`--network host`, an intentionally-unreachable network) — a different problem from "does the current
32-test Aspire/DCP suite run reliably on a GitHub-hosted runner." The current suite has run green, locally,
every time it's been exercised this project's history; whether it holds up on a CI runner specifically is a
real but *separate* unknown, and the empirically-honest way to answer it is a real CI run, not by first
building an unrelated harness on the assumption that it's needed. If a real run does turn out flaky, that's a
concrete, actionable follow-up — not a reason to block this change now.

**Gate `publish-release` (and `base-image-refresh.yml`) by default; `publish-edge` opts out via
`--skip-publish-gate`.**

Edge exists specifically for fast in-development signal on every trunk push; the item's own acceptance
criteria only ever reference "a `vX.Y.Z` release is triggered." Gating every trunk commit on a suite that takes
roughly a minute and a half locally (per this session's own recent runs) would meaningfully slow ordinary
development for a channel whose whole purpose is speed. Since the gate is now the *default* behavior of
`DockerPublish` itself (not something each workflow has to opt into), `publish-edge` needs an explicit
`--skip-publish-gate` to keep its original speed; `base-image-refresh.yml`'s `:latest` republish is left gated
(no flag) since it's infrequent and still ships to real users. The stub's own suggested lighter-touch
alternative for edge/general early signal — a nightly scheduled run — is left as an explicit non-goal, not
built here.

## Risks / Trade-offs

- [Risk] Aspire/DCP genuinely is flakier on a GitHub-hosted runner than locally (network/timing differences
  under a shared/virtualized CI environment). → Mitigation: this is exactly what the real CI run (required as
  part of applying this change, consistent with this project's practice all session of verifying live rather
  than assuming) will surface. If it does, the fix is a follow-up item, informed by real failure data instead
  of speculation — not a reason to gate this change on building the non-DCP harness pre-emptively.
- [Risk] A release now takes longer (Test + E2E + multi-platform build, sequentially) before publishing. →
  Accepted trade-off: this is the whole point (a broken release should not be fast to ship), and it only
  affects the release-tag path, not ordinary development.
