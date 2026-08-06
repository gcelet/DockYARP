---
id: add-renovate-bot
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal — dependency hygiene)
provenance: 2026-08-06 CI/ops backlog expansion
---

## Why
There is no automated dependency maintenance: NuGet packages (CPM), the .NET SDK, GitHub Actions, the Docker base
image, and the docs-site npm/Hugo theme all drift and accumulate CVEs. Renovate opens grouped update PRs that the
CI (`add-ci-build-test`) validates.

## Current state
- No `renovate.json`/Dependabot. Versions are centralized in `Directory.Packages.props` (CPM); the SDK is pinned
  in `global.json`; the base image is in the root `Dockerfile`; the docs-site has `package.json`.

## Proposed change (sketch)
- Add `renovate.json` (or `.github/renovate.json`) covering all managers:
  - **NuGet CPM** (`Directory.Packages.props`), **.NET SDK** (`global.json` → `dotnet-sdk`), **GitHub Actions**,
    **Dockerfile `FROM`** (digest-pinned, ties into `add-base-image-rebuild`), **npm** (docs-site theme).
  - **Grouping** (e.g. group the 6 analyzers, group Aspire, group OpenTelemetry, group xUnit/NUnit test stack),
    a weekly **schedule**, a **dependency dashboard**, and optional automerge for dev-only patch bumps.
  - Respect `TreatWarningsAsErrors` — a bump that breaks the analyzers fails CI, so no automerge on runtime deps.

## Acceptance criteria (→ scenarios)
- **WHEN** a dependency (package / SDK / action / base image / npm) has an update **THEN** Renovate opens a PR per
  the grouping policy, and `add-ci-build-test` validates it.
- **WHEN** `renovate.json` is present **THEN** it validates (`renovate-config-validator`) and covers all five managers.

## Notes / risks / references
- The Renovate **GitHub App** must be installed on the repo — deferred until the repo exists; the **config** can
  be authored + validated now with `npx --yes renovate-config-validator renovate.json`.
- Enables `add-base-image-rebuild` (digest bumps of the `FROM`).
