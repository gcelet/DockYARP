---
id: regroup-renovate-catchall
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal finding — CI/dependency-tooling, not a parity gap)
provenance: same-session follow-up to fix-sonaranalyzer-csharp-upgrade, 2026-08-24 — unblocking the
  rate-limited Renovate backlog surfaced that only 5 of the many pending updates had an explicit group,
  so every other minor/patch/digest bump opened its own PR and paid the full CI pipeline separately
---

## Why
Only 5 `packageRules` groups existed in `renovate.json` (code-quality analyzers, Aspire, OpenTelemetry, gRPC,
test stack). Forcing the rate-limited queue open tonight showed every other update (postcss, BCrypt.Net-Next,
gitversion.tool, the `Microsoft.Build.*`/`Microsoft.Extensions.*.Abstractions` "dotnet monorepo" bundle) opens
its own individual PR, each triggering the full CI pipeline (Test+E2E+DockerImage+SBOM+Trivy) on its own —
unnecessary CI queue pressure for updates that are almost always safe to batch.

## nginx-proxy behavior
N/A — internal CI/dependency-tooling configuration, not a proxy-behavior parity gap.

## DockYarp today
Fixed as part of this same finding: 4 catch-all `packageRules`, grouped by what a dependency actually is
rather than just its manager, placed before the 5 existing named groups (which still win for their own
packages, since Renovate applies `packageRules` in order and a later match overrides `groupName`):
- **`nuget dependencies`** — app runtime nuget packages not claimed by a more specific rule.
- **`build tooling`** — `Fallout.Common`/`Microsoft.Build.*` (`build/_build.csproj`) and `gitversion.tool`
  (`.config/dotnet-tools.json`), scoped via `matchFileNames` — build-time tooling, not shipped in the runtime
  image, kept separate from app dependencies.
- **`npm dependencies`** — docs-site tooling (postcss, ...).
- **`github-actions dependencies`** — CI workflow action versions.

Validated with the real `renovate-config-validator` (not just JSON syntax).

## Proposed change (sketch)
Already implemented directly in `renovate.json` (see above) — this item exists to close the loop per
`AGENTS.md`'s change lifecycle, not to scope future work.

## Acceptance criteria (→ scenarios)
- **WHEN** a non-major nuget update is not claimed by an existing named group **THEN** it is grouped into
  `nuget dependencies` rather than opening its own PR.
- **WHEN** an update touches `build/_build.csproj` or `.config/dotnet-tools.json` **THEN** it is grouped into
  `build tooling`, not `nuget dependencies`, even though both are nuget-sourced.
- **WHEN** `renovate.json` is validated with `renovate-config-validator` **THEN** it passes.

## Notes / risks / references
- Applies going forward only — already-open individual PRs (postcss #4, Aspire #5, BCrypt #6, etc.) are
  unaffected until Renovate re-evaluates/rebases them.
- Trade-off, accepted deliberately: bundling unrelated packages into one PR means a single failing package
  can block automerge for the whole batch — same shape as the `SonarAnalyzer.CSharp` bump blocking the whole
  "code-quality analyzers" group tonight. Acceptable since it directly serves the stated goal (fewer CI runs),
  and a blocked batch still surfaces as a real, actionable PR rather than silently expanding scope.
