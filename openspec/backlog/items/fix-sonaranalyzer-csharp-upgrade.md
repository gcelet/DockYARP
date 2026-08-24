---
id: fix-sonaranalyzer-csharp-upgrade
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal finding — build/code-quality tooling, not a parity gap)
provenance: Renovate PR #2/#3 CI investigation, 2026-08-24 — PR #3 (`renovate/code-quality-analyzers`,
  grouped SonarAnalyzer.CSharp/Roslynator.Analyzers/Meziantou.Analyzer bump) fails Build/Full-gate with ~73
  new diagnostics
---

## Why
Renovate's grouped "code-quality analyzers" PR bumps `SonarAnalyzer.CSharp` from `10.29.0.143774` to
`10.33.0.1635` (alongside `Roslynator.Analyzers` and `Meziantou.Analyzer`). The newer SonarAnalyzer version
activates two diagnostics by default that DockYarp's code does not yet satisfy, and — because
`TreatWarningsAsErrors=true` (`AGENTS.md`) — both become hard build failures:
- **S8969** — "Remove this null-forgiving operator; the compiler already knows this expression is not null
  here." (~65 occurrences, almost all in `tests/DockYarp.Core.Tests` and similar test projects, e.g.
  `ClusterModelTests.cs`).
- **S8949** — "Pass the `context.CancellationToken` to this method to allow cancellation of the operation, or
  use `CancellationToken.None` to opt out explicitly." (a handful of occurrences, e.g.
  `tests/DockYarp.E2E.GrpcBackend/EchoerService.cs`).

As written, this Renovate PR can never go green/automerge on its own — the version bump and the code fixes it
requires have to land together.

## nginx-proxy behavior
N/A — internal .NET build/analyzer tooling, not a proxy-behavior parity gap.

## DockYarp today
`Directory.Packages.props` pins `SonarAnalyzer.CSharp` at `10.29.0.143774`; the affected call sites predate
the stricter nullable-flow proof (S8969) and explicit-cancellation (S8949) checks the newer analyzer version
adds.

## Proposed change (sketch)
Bump the "code-quality analyzers" group to the versions Renovate proposes (this supersedes/absorbs PR #3 —
once this change merges to `develop` at the same version, Renovate should detect the dependency is already
satisfied and auto-close PR #3; that is expected, not a bug), then fix every flagged call site directly — no
`#pragma`/`[SuppressMessage]` per `AGENTS.md`'s guardrail:
- Remove the now-redundant `!` null-forgiving operators (S8969) where the compiler's nullable-flow analysis
  already proves non-null.
- Thread the real ambient `CancellationToken` through the flagged async calls (S8949), falling back to
  `CancellationToken.None` only where no cancellation source genuinely exists in that scope.

## Acceptance criteria (→ scenarios)
- **WHEN** `SonarAnalyzer.CSharp` (+ grouped `Roslynator.Analyzers`/`Meziantou.Analyzer`) is upgraded to the
  versions Renovate PR #3 proposes **THEN** `dotnet build DockYarp.slnx` succeeds with zero warnings.
- **WHEN** a null-forgiving operator is removed **THEN** the surrounding nullable-flow proof still holds (no
  new nullable-warning regression, no NRE risk introduced).
- **WHEN** a flagged async call receives an explicit `CancellationToken` **THEN** it is the real ambient token
  for that call site, not a blanket `CancellationToken.None`, unless no cancellation source exists in scope.

## Notes / risks / references
- ~73 flagged sites at investigation time (2026-08-24) — mostly mechanical (drop `!`), a handful need an
  actual `CancellationToken` threaded through a method signature — verify no public API surface is broken by
  adding a `CancellationToken` parameter to an already-shipped method.
- Confirm after merge that Renovate PR #3 auto-closes rather than re-opening against the new baseline (if it
  doesn't, that's worth a follow-up look, not a re-fix of this item).
