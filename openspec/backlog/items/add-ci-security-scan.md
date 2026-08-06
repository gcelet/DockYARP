---
id: add-ci-security-scan
capability: deployment
agent: AG-DEP
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal — supply-chain security)
provenance: 2026-08-06 CI/ops backlog expansion
---

## Why
No supply-chain security signal: no code scanning, no dependency review, no SBOM, no image vulnerability scan. A
reverse proxy is security-sensitive; these belong in CI once it exists.

## Current state
- No CodeQL, no dependency-review, no SBOM, no Trivy/Grype. (Note: `NuGetAuditMode` is set on the Aspire test
  projects, but that only audits our direct picks at restore.)

## Proposed change (sketch)
- **CodeQL** (C#) on `pull_request` + a weekly schedule.
- **dependency-review-action** on `pull_request` (flag vulnerable/newly-added deps).
- **SBOM** generation (CycloneDX .NET tool, or `anchore/syft`) as a release artifact (feeds `add-ci-image-publish`).
- **Image vulnerability scan** (Trivy or Grype) on the built image, failing on High/Critical (with an allowlist
  for accepted findings).

## Acceptance criteria (→ scenarios)
- **WHEN** a PR is opened **THEN** CodeQL + dependency-review run and surface findings.
- **WHEN** the app image is built **THEN** it is scanned (fail on High/Critical) and an SBOM is produced.

## Notes / risks / references
- **Lower priority / optional first cut.** CodeQL + dependency-review need the repo on GitHub; SBOM + image scan
  can be run locally (`trivy image`, `syft`) and via `act` meanwhile.
- Depends on `add-ci-build-test` (+ `add-ci-image-publish` for the image scan).
