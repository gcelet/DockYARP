# Design — add-renovate-bot

## Managers covered
`renovate.json` at the repo root, extending `config:recommended`:
- **NuGet CPM**: Renovate reads `Directory.Packages.props` natively (all versions live there under CPM).
- **GitHub Actions**: `ci.yml` / `image.yml` action pins.
- **Dockerfile**: the `FROM mcr.microsoft.com/dotnet/aspnet:10.0` base image, **`pinDigests: true`** so the
  `FROM` is digest-pinned and Renovate keeps the digest fresh — the hook `add-base-image-rebuild` builds on.
- **npm**: the docs-site theme (`docs-site/package.json`).

## Grouping + hygiene
- Group the 6 analyzers, `Aspire*`, `OpenTelemetry*`, the NUnit/test stack, and the gRPC fixture packages into
  single PRs (fewer, coherent PRs; the strict analyzers mean an analyzer bump must pass CI, so no automerge on
  runtime deps).
- Weekly schedule, a dependency dashboard, and the project's gitmoji + `chore:` commit convention
  (semantic commits disabled): `:arrow_up: chore: upgrade …`, with `:pushpin: chore: pin …` for pins/digest pins
  and `:arrow_down: chore: downgrade …` for rollbacks. Major updates never automerge.

## Keep the SDK floor relaxed
`global.json` was deliberately relaxed to accept any installed .NET 10 (`10.0.100` + `latestMinor`). Renovate
MUST NOT re-pin it — a `packageRule` disables the `dotnet-version` datasource, so the "any .NET 10" floor stands.

## Validation
`renovate-config-validator renovate.json` (via npx) validates the schema locally. Renovate only *runs* once its
GitHub App is installed on the repo (which does not exist yet) — deferred; the config is correct + validated now.

## Out of scope
- Automerge policies (kept conservative — CI-gated review), and the actual base-image rebuild trigger
  (`add-base-image-rebuild`).
