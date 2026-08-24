## Why

Renovate's grouped "code-quality analyzers" PR (`SonarAnalyzer.CSharp` 10.29.0.143774 → 10.33.0.1635, plus
`Roslynator.Analyzers` and `Meziantou.Analyzer`) cannot go green/automerge on its own: the newer
SonarAnalyzer version activates two diagnostics DockYarp's code does not yet satisfy, and
`TreatWarningsAsErrors=true` (`AGENTS.md`) turns both into build failures. A real local build of the
Renovate branch initially found 18 unique flagged sites in the projects MSBuild could reach; a full-solution
build after fixing those revealed **33 unique sites in total** — `DockYarp.Tls`'s own 2 sites had been
blocking MSBuild from even attempting `DockYarp.App` and every test project depending on it, hiding their
diagnostics until fixed (not the ~73 first estimated from a duplicated CI log either way) — small enough to
fix directly rather than leave the PR permanently blocked.

## What Changes

- Bump the "code-quality analyzers" group to the versions Renovate PR #3 proposes (this change supersedes
  that PR; once merged at the same version, Renovate should auto-close it as satisfied).
- Fix all 18 flagged sites — no `#pragma`/`[SuppressMessage]`, per `AGENTS.md`'s guardrail:
  - **S8969** ("remove this null-forgiving operator; the compiler already knows this is not null") — 31
    sites, almost all a `!` made redundant by a preceding `X.Should().NotBeNull()` (or equivalent) assertion
    narrowing the compiler's own nullable flow for the rest of the method.
  - **S8949** ("pass the CancellationToken instead of an implicit none") — 4 sites, each with a real ambient
    token available (`context.RequestAborted` / `context.CancellationToken`).

## Capabilities

Pure code-quality/build-tooling upgrade — no product-facing behavior changes. `skip_specs: true` is set in
this change's `.openspec.yaml` (no capability deltas).

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `Directory.Packages.props` — version bump for the 3 grouped packages.
- `src/DockYarp.Tls/TlsDomains.cs` (2 sites, S8969), `src/DockYarp.Tls/Http01ChallengeMiddleware.cs`
  (1 site, S8949), and `src/DockYarp.App/ErrorPages/ErrorPageMiddleware.cs` (1 site, S8949) — the only
  production-code files affected.
- `tests/DockYarp.E2E.GrpcBackend/EchoerService.cs` (1 site, S8949) — E2E test-support backend.
- 12 test files (28 sites, all S8969): `ClusterModelTests.cs`, `ContainerMapperTests.cs`,
  `DockerFiltersTests.cs`, `DockerTlsCredentialsTests.cs`, `LabelParserTests.cs`,
  `RestartPersistenceTests.cs`, `TlsTests.cs`, `CertificateStoreTests.cs` (8 sites),
  `HtpasswdStoreTests.cs`, `DataProtectionSetupTests.cs`, `ClientIpHashSessionAffinityPolicyTests.cs`,
  `YarpConfigMapperTests.cs`.
