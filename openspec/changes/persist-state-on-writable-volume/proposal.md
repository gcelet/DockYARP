## Why
DockYarp runs **non-root** (chiseled image), and its persistent state — ACME certificates and, since Data
Protection is registered transitively via YARP, the DP keys — must survive container recreation. Today the app
writes to `/app/certs` (the app-writable content root), **not** the declared `/certs` volume, because a
Docker-created `/certs` volume is **root-owned** and a non-root process cannot write to it. So nothing actually
persists across `docker compose down/up`. This makes the mounted volume writable by the app user and points
DockYarp at it, so certificates and Data Protection keys persist.

## What Changes
- **Dockerfile**: create `/certs` owned by the app user (`COPY --chown=$APP_UID` of an empty dir seeded in the
  build stage — the chiseled runtime has no shell for `chown`), and set `ENV Tls__CertificateDirectory=/certs`.
- **Program.cs**: persist Data Protection keys to `<CertificateDirectory>/dataprotection-keys`.
- **docker-compose**: use a **named volume** for `/certs` (inherits the image's app ownership → non-root-writable
  and persistent) instead of a host bind mount.
- **e2e**: bind-mount a world-writable host directory at `/certs` so the non-root container can write, exercising
  the real persistent path.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: persistent state (certificates + Data Protection keys) is written to a non-root-writable
  mounted volume and survives container recreation.

## Impact
- **Code/infra**: `Dockerfile`, `docker-compose.yml`, `src/DockYarp.App/Program.cs`,
  `tests/DockYarp.E2E.AppHost/*`, `tests/DockYarp.E2E.Tests/TlsHarness.cs`, `docs/deployment.md`.
- **Deferred**: an e2e **restart**-persistence test (provision → recreate the container → assert the cert/key
  survives) — a follow-up backlog item; and at-rest DP-key encryption (the "no encryptor" note).
- **Owning agent**: AG-DEP.
- **Runtime**: validated by the next `E2E` run — DockYarp must still provision certificates (proving the
  non-root app can write the mounted `/certs`), and `dockyarp.log` shows no ephemeral-DP-keys warning.
