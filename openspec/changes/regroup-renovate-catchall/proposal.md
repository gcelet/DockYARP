## Why

Only 5 `packageRules` groups existed in `renovate.json` (code-quality analyzers, Aspire, OpenTelemetry, gRPC,
test stack). Unblocking tonight's rate-limited Renovate queue showed every other minor/patch/digest update
(postcss, BCrypt.Net-Next, gitversion.tool, the `Microsoft.Build.*`/`Microsoft.Extensions.*.Abstractions`
bundle) opens its own individual PR, each paying the full CI pipeline (Test+E2E+DockerImage+SBOM+Trivy)
separately — unnecessary CI queue pressure for updates that are almost always safe to batch.

## What Changes

- Add 4 catch-all `packageRules` to `renovate.json`, grouped by what a dependency actually is, not just its
  manager: `nuget dependencies`, `build tooling` (scoped via `matchFileNames` to `build/_build.csproj` and
  `.config/dotnet-tools.json`), `npm dependencies`, `github-actions dependencies`.
- Placed before the 5 existing named groups, which still win for their own packages (Renovate applies
  `packageRules` in order; a later match overrides `groupName`).

## Capabilities

Pure CI/dependency-tooling configuration — no product-facing behavior changes. `skip_specs: true` is set in
this change's `.openspec.yaml` (no capability deltas).

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `renovate.json` only.
- No source/test code affected — no build/test verification needed beyond validating the config itself.
