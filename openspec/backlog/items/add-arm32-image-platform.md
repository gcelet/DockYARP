---
id: add-arm32-image-platform
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: n/a (DockYarp packaging/ops — nginx-proxy's own images cover a similar arch matrix, no direct parity claim)
provenance: 2026-08-17 user request, checked against existing backlog/archive first (not already tracked)
---

## Why
The user asked for a multi-platform image covering x64, arm64, **and 32-bit ARM if possible** — targeting
older/smaller ARM single-board computers (e.g. 32-bit Raspberry Pi models) common in the self-hosted/home-lab
audience DockYARP targets. Checked the backlog and archived changes first, per habit: **x64 + arm64 are already
live** (`linux/amd64,linux/arm64`, wired in `.github/workflows/image.yml` via the already-archived
`add-ci-image-publish`); no existing item covers 32-bit ARM specifically — this is a genuine, new gap, not a
duplicate.

## nginx-proxy behavior
N/A — image platform matrix is a DockYarp packaging decision, not a proxy feature. No `parity.md` row.

## DockYarp today
- `build/Build.cs`'s `Platforms` parameter is a free-form comma-separated string (`"linux/amd64"` default,
  already used as `"linux/amd64,linux/arm64"` in CI) — buildx itself has no code-level restriction to two
  platforms; adding a third is mechanically just extending the string passed to `--platforms` in
  `.github/workflows/image.yml` (both the release and `--edge` jobs).
- **Not yet verified**: whether `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` (the runtime base image,
  `Dockerfile:17`) is actually published for `linux/arm/v7` at all. Chiseled images trim components aggressively
  and Microsoft's chiseled variant manifest lists have historically been narrower than the regular
  Debian/Ubuntu images (which do cover arm32 in some cases) — this needs a direct check (`docker manifest
  inspect mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` or the MCR catalog page) before committing to
  this item's feasibility, not an assumption either way.
- **Same-day, directly relevant finding** (2026-08-17, same session): a real CI failure on the existing
  amd64+arm64 matrix showed `qemu : error : uncaught target signal 11 (Segmentation fault)` — caused by
  unrelated E2E/test projects with native `Grpc.Tools` binaries being built inside the image stage (now fixed,
  see the `Publish`-scoping commit). That specific crash is resolved, but it's a live reminder that **QEMU
  emulation for less-common platforms is a real, not theoretical, risk** for this project — a third platform
  (32-bit ARM, likely the least-tested emulation target of the three) should be validated with a real registry
  push before being trusted, not just assumed to work because the buildx command accepts the platform string.

## Proposed change (sketch)
1. **Feasibility check first**: confirm the chiseled base image ships for `linux/arm/v7` (and note .NET's own
   support policy for 32-bit ARM on this OS/version combo — some .NET features/packages have reduced or no
   32-bit ARM support). If it doesn't, this item closes as won't-do / needs a non-chiseled base image trade-off
   (a design decision, not a default fallback to silently swap the base image).
2. If feasible: extend `--platforms` in both `image.yml` jobs (release + `--edge`) to
   `linux/amd64,linux/arm64,linux/arm/v7`.
3. Real registry push validation (this backlog's own precedent from earlier today: don't trust a multi-arch
   build until it has actually pushed successfully) — a real `DockerPublish` run against a throwaway registry
   tag, not just a dry-run/local `--load` (buildx `--load` doesn't support multi-platform manifests anyway, so
   this genuinely needs a `--push`).
4. Consider whether 32-bit ARM needs its own CI job/step timing budget — QEMU-emulated 32-bit ARM builds are
   often the slowest of a mixed-arch matrix.

## Acceptance criteria (→ scenarios)
- **WHEN** the release or edge publish workflow runs **THEN** the pushed image manifest includes
  `linux/amd64`, `linux/arm64`, and `linux/arm/v7` (assuming step 1 confirms feasibility).
- **WHEN** the base image does not support `linux/arm/v7` **THEN** the item is closed with the finding recorded
  (not silently dropped) rather than left ambiguously open.

## Notes / risks / references
- Refs: `build/Build.cs` (`Platforms` parameter, `DockerPublish` target), `.github/workflows/image.yml` (both
  `--platforms` call sites), `Dockerfile:17` (base image).
- Related, same-session context: the `Publish`-target scoping fix (2026-08-17) that resolved a real QEMU
  segfault on the existing arm64 leg — read that commit/finding before starting this item, it's directly
  relevant risk context, not just background.
