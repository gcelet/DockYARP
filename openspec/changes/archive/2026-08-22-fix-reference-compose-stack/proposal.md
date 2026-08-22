## Why

`docker-compose.yml` (repo root) is README's own "Quick start" entry point — a real landing point for people
who never visit the docs site. Investigated live on 2026-08-22 (not assumed): its published-image reference is
wrong (points at Docker Hub, where the image doesn't exist — the real target is GHCR), it mounts a volume that
currently does nothing, and it never demonstrates TLS/ACME or the admin dashboard. See
`openspec/backlog/items/fix-reference-compose-stack.md` for the full investigation.

## What Changes

- Fix `image: gcelet/dockyarp` → `image: ghcr.io/gcelet/dockyarp` everywhere it's wrong: `docker-compose.yml`
  and the 5 docs-site pages that repeat the same incorrect reference.
- Remove the dead `./config:/config` mount from `docker-compose.yml`'s live `dockyarp` service (nothing reads
  it — `StaticConfig:Path` is never set); replace with a short comment pointing at `StaticConfig:Path` as an
  alternate/no-Docker config source, linking the docs.
- Add a commented-out (inactive by default) `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL` example to
  `docker-compose.yml`, so a reader sees how automatic HTTPS is turned on without it actually running (and
  failing/retrying) against the non-public `whoami.local` demo domain.
- Add a comment near `docker-compose.yml`'s `AdminApi__*` env vars showing `AdminApi__Surface: "ApiAndDashboard"`
  as the alternative to the current `"Api"` (dashboard included).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — this is a docs/example completeness fix with no DockYarp behavior change; `skip_specs: true` is set in
this change's `.openspec.yaml`)

## Impact

- `docker-compose.yml` — image reference, volume, TLS example, dashboard comment.
- `docs-site/content/en/docs/getting-started.md`, `deployment.md`, `examples.md`, `migrating-from-nginx-proxy.md`
  — image reference only (5 occurrences across 4 files).
- No `DockYarp.slnx` build impact — pure docs/example edits, no source changes.
