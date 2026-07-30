---
id: add-doc-ci-publish
capability: documentation
agent: AG-DOC
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: (internal initiative — documentation portal, no parity row)
provenance: user idea 2026-07-30 (doc-site family)
---

## Why
The documentation site is only valuable if it is published and stays current. Automating the build and
publication (reproducibly, in CI) removes manual steps and keeps the live site in sync with `main`.

## nginx-proxy behavior
N/A — internal initiative. No `parity.md` row.

## DockYarp today
- No Nuke target builds or publishes the docs; no CI job for documentation. The foundation site builds only via
  a local command.

## Proposed change (sketch)
- Add a Nuke `Docs` target that builds the static site reproducibly (pinned/containerized Hugo Extended binary,
  no ambient dependency), and a `PublishDocs` target that deploys the output to the configured static host.
- Publish to **GitHub Pages** by default; keep the host swappable via configuration.
- Add a CI workflow that runs `Docs` on every PR (build check) and `PublishDocs` on merge to `main`.
- Ensure the docs build is isolated from the application build (`DockYarp.slnx`) to avoid cross-contamination.

## Acceptance criteria (→ scenarios)
- **WHEN** `nuke Docs` runs **THEN** a complete static site is produced from a pinned/containerized Hugo, with no
  ambient toolchain dependency.
- **WHEN** `nuke PublishDocs` runs **THEN** the site is deployed to the configured target (GitHub Pages by default).
- **WHEN** a PR changes documentation **THEN** CI builds the site; **WHEN** it merges to `main` **THEN** CI
  republishes.

## Notes / risks / references
- Depends on [[add-doc-site-foundation]] (and benefits from [[add-doc-brand-theme]], [[add-doc-search]],
  [[add-doc-versioning]] when present).
- Tier B-runtime: needs an external CI runner + hosting (GitHub Pages), unlike the deterministic in-repo items.
- Do not touch the Nuke stop-files (`build/Directory.Build.*`); add the target within the existing build project.
- **Docsy install method to revisit here**: the foundation installs Docsy as a **Git submodule** (avoids Go on
  the Windows dev box). In CI this is a one-liner (`actions/checkout` with `submodules: recursive`), so it is
  not a blocker. When building CI, decide whether to keep the submodule or switch to **Hugo Modules** (Go is
  trivial to add on a CI runner) — a project preference, not a technical constraint. The user finds the local
  submodule mildly annoying but acceptable for now.
