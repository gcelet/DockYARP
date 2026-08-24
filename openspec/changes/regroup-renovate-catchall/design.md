## Context

See `proposal.md` for the motivating CI-queue-pressure finding. `renovate.json`'s `packageRules` are applied
in array order, and a later matching rule overrides an earlier one's `groupName` for the same package — this
is the mechanism every decision below relies on.

## Goals / Non-Goals

**Goals:**
- Cut the number of separate Renovate PRs (and therefore separate CI pipeline runs) for routine non-major
  updates, without merging semantically unrelated ecosystems into one PR.
- Keep build-time tooling (`Fallout.Common`, `Microsoft.Build.*`, `gitversion.tool`) separate from app runtime
  dependencies — a failure in one should not block the other.

**Non-Goals:**
- Not touching the existing 5 named groups (analyzers, Aspire, OpenTelemetry, gRPC, test stack) — they already
  group by logical concern correctly.
- Not changing `schedule`, `automerge`, or `prHourlyLimit`-driven behavior — this is purely about grouping.

## Decisions

**4 catch-alls by logical category, not 1 flat "everything" bucket.** A single bucket (considered first, see
Alternatives) would mix docs-site npm tooling with backend nuget packages with CI workflow actions in one PR —
a failure in an unrelated ecosystem would block all of them together for no good reason. Splitting by what the
dependency *is* (app runtime vs. build tooling vs. docs tooling vs. CI actions) keeps failures scoped to a
meaningful boundary.

**`build tooling` scoped via `matchFileNames`, not a hardcoded package-name list.** `Fallout.Common` and
`Microsoft.Build.*` are both nuget-sourced, so a manager-only rule can't distinguish them from app dependencies
like `Microsoft.Extensions.*.Abstractions`. `matchFileNames: ["build/_build.csproj", ".config/dotnet-tools.json"]`
scopes by *where* the dependency is declared instead — the Fallout build project and the dotnet-tools manifest
are exactly the build-time-only surface, without needing to maintain an explicit package list that drifts as
new build tooling is added.

**Alternative considered — one flat "other dependencies" bucket (first draft this session)**: rejected after
user feedback — mixes unrelated ecosystems, making a single failure block an oversized, semantically
meaningless batch.

**Alternative considered — `groupName: "other {{manager}}dependencies"` templating (second draft)**: closer,
splits by manager automatically without hardcoding, but still lumps build-tooling nuget packages in with app
nuget packages since both share the `nuget` manager. Rejected in favor of the explicit `build tooling` rule,
which captures the *reason* the split matters (does it ship in the runtime image?) rather than a surface-level
technical grouping.

## Risks / Trade-offs

- [Risk] Bundling unrelated packages within the same category still means one failing package can block
  automerge for the whole batch (same shape as tonight's `SonarAnalyzer.CSharp` blocking the whole
  "code-quality analyzers" group) → Mitigation: accepted deliberately — it directly serves the stated goal
  (fewer CI runs), and a blocked batch still surfaces as one real, actionable PR rather than silently
  expanding scope or hiding a failure.
- [Risk] Already-open individual PRs (postcss, Aspire, BCrypt, ...) do not retroactively merge into the new
  groups → Mitigation: not a real risk, just a timing note — they resolve on Renovate's next
  re-evaluation/rebase of those branches.
