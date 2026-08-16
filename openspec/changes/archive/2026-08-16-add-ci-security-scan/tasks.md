## 1. CodeQL (AG-DEP)

- [x] 1.1 Add `.github/workflows/codeql.yml`: trigger `pull_request:` (no base-branch filter, matching
      `ci.yml`) + `schedule:` (weekly).
- [x] 1.2 `github/codeql-action/init@v4` with `languages: csharp`, `build-mode: manual`.
- [x] 1.3 Build step between `init` and `analyze`: `./build.sh Compile` (no `/p:UseSharedCompilation=false` —
      confirmed not needed for .NET Core 3.0+, see design.md).
- [x] 1.4 `github/codeql-action/analyze@v4`.

## 2. Dependency review (AG-DEP)

- [x] 2.1 Add `.github/workflows/dependency-review.yml`: trigger `pull_request:`, single step
      `actions/dependency-review-action@v5` with `fail-on-severity: moderate`.

## 3. SBOM + image scan (AG-DEP)

- [x] 3.1 Add a root `.trivyignore`, empty, with a comment explaining how to allowlist an accepted finding.
- [x] 3.2 `image.yml` `publish-release` job: after the Nuke build-and-push step, added `anchore/sbom-action@v0`
      (`image: {registry}/{repository}:{version}`, `format: spdx-json`, `upload-artifact: true`) and
      `aquasecurity/trivy-action@v0.36.0` (`image-ref` the same reference, `severity: CRITICAL,HIGH`,
      `exit-code: 1`, `trivyignores: .trivyignore`).
- [x] 3.3 `image.yml` `publish-edge` job: added the same Trivy scan step (image reference
      `{registry}/{repository}:edge`); no SBOM step (release-only per design.md).

## 4. Validation (AG-DEP)

- [x] 4.1 Validated all three new/edited workflow YAML files parse (no `actionlint` on this machine per prior
      sessions — `yaml.safe_load` via `uvx --with pyyaml`); confirmed the expected job names in each.
- [x] 4.2 Run `npx @fission-ai/openspec@latest validate add-ci-security-scan --strict`.
