## Context

See `proposal.md` - Why. Verified before writing (same discipline as the `add-release-changelog` tool pivot
earlier this session — check third-party action behavior rather than guess):
- **CodeQL manual build mode + C#**: confirmed `build-mode: manual` is supported for compiled languages
  including C#, letting the workflow run its own build command between `init` and `analyze` instead of
  CodeQL's `autobuild`. Checked whether the commonly-cited `/p:UseSharedCompilation=false` workaround (needed
  because MSBuild's shared-compiler-server process defeats CodeQL's build tracer) applies here — GitHub's own
  docs state **it is not necessary for .NET Core 3.0 and later**. DockYarp targets .NET 10 (SDK-style, not
  .NET Framework), so this workaround is skipped; `./build.sh Compile` is used unmodified.
- **Action versions**, checked against each project's current docs rather than assumed:
  `github/codeql-action/{init,analyze}@v4`, `actions/dependency-review-action@v5` (`fail-on-severity` input),
  `anchore/sbom-action@v0` (`image`/`format`/`upload-artifact` inputs), `aquasecurity/trivy-action@v0.36.0`
  (`image-ref`/`severity`/`exit-code`/`trivyignores` inputs — this action pins a specific version, not a
  floating major tag, unlike the others).
- `image.yml` (from `add-image-tag-strategy`, this session) now has two publish jobs, both pushing a real image
  to the registry via the single Nuke `DockerPublish` path — both are natural attachment points for a
  post-push SBOM/scan step, since both already produce a real, registry-addressable image.
- `ci.yml`'s `pull_request:` trigger has no base-branch filter (fires on any PR) — mirrored here for consistency
  rather than inventing a different scoping rule for the new workflows.

## Goals / Non-Goals

**Goals:**
- All four mechanisms from the backlog stub: CodeQL, dependency review, SBOM, image scan.
- Scan the image CI **already pushed**, not a separate rebuild — no duplicated build logic, and the scan result
  reflects exactly what got published.

**Non-Goals:**
- Attaching the SBOM to the GitHub Release itself (`upload-release-assets`) — that input's behavior depends on
  running during a GitHub `release` event; `image.yml` triggers on `push: tags`/`branches`, not `release:
  published`, and the behavior wasn't confirmed to still apply there. Uploading as a plain workflow artifact
  (`upload-artifact: true`) is unambiguous and sufficient for this item; wiring it into the Release page can be
  a follow-up once actually needed.
- A `NuGetAuditMode`-style change to restore-time auditing — already partially covered per the stub's "Current
  state" note (Aspire test projects), and full-repo restore-time auditing is a different mechanism (advisory at
  restore vs. CodeQL/Trivy's static/image analysis), not this item's scope.
- Any actual allowlisted CVE entries in `.trivyignore` — the file is added empty (with usage instructions);
  populating it is a real security decision made when a specific finding needs it, not invented here.

## Decisions

- **CodeQL builds via `./build.sh Compile`, not `autobuild`.** Keeps the single Nuke build path
  ([[nuke-single-build-path]]) — the same command every other workflow already uses, rather than trusting
  CodeQL's own solution-detection heuristics.
- **Dependency review: `fail-on-severity: moderate`.** Stricter than the action's own default (`low`, which
  would flag nearly everything) but still catches real risk; matches the stub's general intent without
  inventing a stricter-than-asked threshold.
- **SBOM + Trivy scan attach to `image.yml`'s existing jobs, scanning by registry reference** (e.g.
  `{registry}/{repository}:{version}` for the release job, `{registry}/{repository}:edge` for the edge job) —
  both already `docker login`'d in the same job, so no extra auth wiring; no local `--load` needed since these
  tools pull-and-inspect a remote reference directly.
- **Both publish jobs get the Trivy scan; only the release job gets SBOM upload.** Matches the stub's own
  phrasing ("scanned" is general; SBOM is explicitly "a release artifact"). Scanning `edge` too catches
  vulnerabilities as early as the in-development channel, not only at release time.
- **CodeQL + dependency-review triggers mirror `ci.yml`'s bare `pull_request:`** (no base-branch filter) —
  one consistent PR-trigger shape across the repo's security/quality workflows, not a bespoke rule per file.

## Risks / Trade-offs

- [CodeQL adds real CI time on every PR] → acceptable; it's the standard cost of static analysis, and it runs
  in parallel with `ci.yml`'s build-and-test job, not serialized after it.
- [Trivy failing the workflow on a Critical/High finding could block a release with no immediate fix available]
  → mitigated by `.trivyignore` (added, empty) as the documented escape hatch — a deliberate allowlist entry,
  not disabling the scan.
- [`anchore/sbom-action`'s exact version tag `@v0` is a floating major, unlike Trivy's pinned patch version] →
  intentional per each project's own documented convention; not something to force into consistency across
  third-party actions that don't share a versioning scheme.
