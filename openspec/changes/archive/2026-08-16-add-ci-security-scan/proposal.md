## Why

DockYarp is a reverse proxy — internet-facing, security-sensitive — and today has zero supply-chain security
signal in CI: no static code scanning, no dependency vulnerability review, no SBOM, no image vulnerability scan.
The repo now exists on GitHub, unblocking the parts of this item that need it (CodeQL, dependency review).

## What Changes

- Add `.github/workflows/codeql.yml`: CodeQL (C#) on `pull_request` (to `develop`) + a weekly schedule, using
  `build-mode: manual` invoking the same `./build.sh Compile` every other workflow uses (no duplicated build
  logic — [[nuke-single-build-path]]).
- Add `.github/workflows/dependency-review.yml`: `actions/dependency-review-action` on `pull_request`, failing
  on `moderate`+ severity.
- Extend `image.yml`'s two publish jobs (`publish-release`, `publish-edge`) with, after the existing Nuke
  build-and-push step: an SBOM generated from the pushed image (`anchore/sbom-action`, uploaded as a workflow
  artifact) and a vulnerability scan of the pushed image (`aquasecurity/trivy-action`, failing on
  Critical/High), both scanning the image **by registry reference** (no separate re-build — they inspect what
  `DockerPublish` already pushed).
- Add a root `.trivyignore` (empty, with a comment on how to use it) so an accepted finding can be allowlisted
  without editing the workflow.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: adds a **Supply-chain security scanning** requirement (CodeQL, dependency review, SBOM, image
  vulnerability scan), alongside the existing CI/CD requirements.

## Impact

- New: `.github/workflows/codeql.yml`, `.github/workflows/dependency-review.yml`, `.trivyignore`.
- Modified: `.github/workflows/image.yml` (SBOM + scan steps added to both publish jobs).
- No `src/`/`tests/` changes — CI-only (AG-DEP).
