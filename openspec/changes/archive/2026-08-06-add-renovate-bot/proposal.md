## Why
There is no automated dependency maintenance: NuGet packages (CPM), GitHub Actions, the Docker base image, and
the docs-site npm packages drift and accumulate CVEs. Renovate opens grouped update PRs that the CI gate
(`add-ci-build-test`) validates, and — by digest-pinning the `Dockerfile` `FROM` — enables `add-base-image-rebuild`.

## What Changes
- Add `renovate.json` covering every manager: **NuGet CPM** (`Directory.Packages.props`), **GitHub Actions**,
  the **`Dockerfile` base image** (pinned to a digest and updated), and **npm** (docs-site).
- **Grouping** (the 6 analyzers; Aspire; OpenTelemetry; the NUnit/test stack; the gRPC fixture), a weekly
  **schedule**, a **dependency dashboard**, and the project's gitmoji + `chore:` commit convention
  (`:arrow_up: chore: upgrade …`, `:pushpin: chore: pin …`, `:arrow_down: chore: downgrade …`; majors never automerge).
- **The relaxed .NET SDK floor is preserved**: Renovate does **not** re-pin `global.json` (we deliberately set it
  to any installed .NET 10 — re-pinning would defeat that).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: dependencies are kept current via Renovate across NuGet/Actions/Dockerfile/npm, with grouped PRs.

## Impact
- **Code**: `renovate.json` (new). No product code.
- **Validation**: `renovate-config-validator` locally (node available). The Renovate **GitHub App** must be
  installed on the repo to actually run — deferred until the repo exists; the config is authored + validated now.
- **Owning agent**: AG-DEP. Resolves `add-renovate-bot` (feeds `add-base-image-rebuild` via digest pinning).
