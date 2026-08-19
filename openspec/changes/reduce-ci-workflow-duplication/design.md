## Context

See `proposal.md` — Why. Current concrete state, read directly from the workflow files (not re-derived):

- `.github/workflows/ci.yml` already caches NuGet restore: `actions/cache@v6`, `path: ~/.nuget/packages`,
  `key: nuget-${{ runner.os }}-${{ hashFiles('Directory.Packages.props', 'global.json') }}`,
  `restore-keys: nuget-${{ runner.os }}-`. `image.yml` has **no** cache step in either job.
- `image.yml`'s top-level `concurrency: group: image-${{ github.ref }}` applies to the whole workflow run
  (both `publish-release` and `publish-edge` jobs share it). A `push` to `develop` and a `workflow_dispatch`
  targeting `develop` compute the **same** group value (`github.ref` is `refs/heads/develop` either way), so
  they queue behind each other (`cancel-in-progress: false`) even though they're unrelated requests.
- GitHub Actions cache scope is per-repository (and branch-aware via fallback), not per-workflow — a cache
  entry saved by `ci.yml` is visible to `image.yml`'s restore step (and vice versa) as long as the key matches,
  with no extra wiring needed.

## Goals / Non-Goals

**Goals:**
- Two unrelated CI requests on the same ref (an auto-triggered push publish and a manual `workflow_dispatch`)
  stop queuing behind each other.
- The redundant NuGet restore work `ci.yml` and `image.yml` each do on every `develop` push shares a cache
  instead of each downloading packages independently.

**Non-Goals:**
- Eliminating redundant *compilation* — the two workflows genuinely build different things (`ci.yml`: the whole
  solution incl. tests; `image.yml`'s Dockerfile build stage: `DockYarp.App`'s publish graph only, inside a
  separate Docker build context that doesn't share the runner's `bin`/`obj`). Sharing compiled output across a
  host-runner build and a Docker BuildKit build would need a fundamentally different mechanism (e.g. BuildKit
  cache mounts, or restructuring the Dockerfile to accept a pre-built artifact) — out of scope for this low
  priority, efficiency-only change. Restore caching is the achievable win without that redesign.
- Making `publish-edge` depend on `ci.yml`'s success (proposal option 1, not chosen) — would remove the
  duplicated *compile* too, but at the cost of a sequential pipeline, slowing every trunk push's edge image.
  Explicitly rejected by the user when picking this change's direction.
- Any change to `build/Build.cs` or Nuke targets — this only touches workflow-level triggers/caching, matching
  the existing division of labor (Nuke owns build/publish logic).

## Decisions

**Narrow `image.yml`'s concurrency group by adding `github.event_name`:
`group: image-${{ github.ref }}-${{ github.event_name }}`.**

Rationale: the minimal change that separates "push-triggered" from "manually dispatched" runs on the same ref
without weakening either guarantee — pushes on the same ref still serialize against each other (e.g. two rapid
pushes to `develop`), and manual dispatches still serialize against each other (e.g. avoiding two people
re-running a release publish for the same tag concurrently), but the two categories no longer block each other.
Considered and rejected: dropping the concurrency group entirely for `workflow_dispatch` (via a conditional
expression) — more complex to express correctly in the group key and removes a real guard (two people
re-dispatching the same release tag concurrently) for no benefit the simpler fix doesn't already give.

**Add the identical `actions/cache@v6` NuGet step from `ci.yml` to both jobs in `image.yml`, same key scheme.**

Rationale: matching the key exactly (`nuget-${{ runner.os }}-${{ hashFiles('Directory.Packages.props',
'global.json') }}`) is what makes the cache shared across workflows — GitHub Actions cache lookup is by key
within the repository, not scoped to the workflow file that created it. No new key scheme to design or
document; reuses the one `ci.yml` already has. First run after a dependency bump still misses (nothing to
restore from yet) in all three jobs; every push after that hits the shared entry regardless of which workflow
saved it. Considered and rejected: a separate cache key per workflow — would defeat the entire point (no
sharing), and gains nothing `ci.yml`'s existing key doesn't already provide.

## Risks / Trade-offs

- [Risk] Compilation itself still runs twice on every `develop` push (Non-Goal, not fixed here) — the
  duplication is reduced, not eliminated. → Accepted: matches the chosen direction (options 2+3, not option 1);
  revisit only if restore-time savings prove insufficient in practice.
- [Risk] `actions/cache@v6` has a per-repository storage quota (evicts least-recently-used entries once
  exceeded) — three cache-writing jobs now instead of one increases write frequency for the *same* key (not
  new keys), so no meaningful extra quota pressure; a cache-key collision race (two jobs finishing restore
  before either has saved) just means both fall back to a full restore that run, not a correctness issue.
- [Risk] The narrowed concurrency group could let a `push` run and a `workflow_dispatch` run genuinely execute
  in parallel on the same ref, e.g. both trying to publish `edge`-equivalent tags at once. → Assessed as safe:
  `publish-edge` only ever runs on `push` (its `if:` already excludes `workflow_dispatch`), and
  `publish-release`'s tag/version resolution is deterministic from the ref/input, so two concurrent runs would
  push the same content to the same tag, not conflicting content — a harmless redundant push, not a race that
  corrupts state.
