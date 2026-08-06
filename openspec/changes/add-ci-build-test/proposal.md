## Why
The project has **no continuous integration**: pull requests and pushes are not built or tested automatically,
so a broken build (a red build here — warnings are errors) or a failing test can land unnoticed. This is the
foundation every other CI/ops item gates on.

## What Changes
- Add `.github/workflows/ci.yml` that runs on `pull_request` and `push` to `main`:
  - checks out the repo, sets up .NET from `global.json` (relaxed to accept **any installed .NET 10** SDK —
    `version 10.0.100`, `rollForward latestMinor` — so it works across machines without a pinned feature band),
    caches NuGet, and runs the Nuke `Test` gate
    (`./build.sh Test`) on `ubuntu-latest` — compile the whole solution + run unit/integration tests, no Docker.
  - A build warning (warnings-as-errors) or a test failure fails the check.
- The end-to-end suite (needs a Docker daemon) is out of scope of this gate.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: a continuous-integration gate builds and tests the solution on every PR and push.

## Impact
- **Code**: `.github/workflows/ci.yml` (new). No product code.
- **Validation**: the GitHub repo does not exist yet, so the workflow is validated locally with **`act`**
  (nektos/act) and **`actionlint`**; on push (once the repo exists) the check runs for real.
- **Owning agent**: AG-DEP. Resolves `add-ci-build-test` (foundation for `add-ci-image-publish`,
  `add-renovate-bot`, `add-ci-security-scan`, `add-release-versioning`).
