## Context
The runtime image is `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`, which runs as the non-root app user
(`$APP_UID`, 1654) and has **no shell**. Docker creates a `VOLUME` mount point owned by **root**, so a non-root
process cannot write to `/certs`. Certificates currently persist to `/app/certs` (app-writable content root, but
NOT the mounted volume) → lost on container recreation. `FileCertificateStore` writes per-host files under
`TlsOptions.CertificateDirectory`; DP keys (this change) go to `<dir>/dataprotection-keys`.

## Goals / Non-Goals
- **Goal**: the non-root app writes its persistent state to a mounted volume that survives container recreation.
- **Non-Goal**: running as root (rejected); an e2e restart-persistence test (follow-up); at-rest DP-key
  encryption (follow-up).

## Decisions
- Create `/certs` owned by `$APP_UID` in the image: `mkdir` an empty seed dir in the SDK **build** stage (has a
  shell), then `COPY --chown=$APP_UID:$APP_UID --from=build` it into the chiseled runtime **before** the
  `VOLUME` declaration. A Docker named/anonymous volume then inherits that app ownership (writable + persistent).
  Set `Tls__CertificateDirectory=/certs`.
- **docker-compose**: use a **named volume** (`certs:/certs`) rather than a host bind mount — a bind mount would
  re-impose the host directory's ownership and break the non-root write; a named volume inherits the image's
  app-owned `/certs`.
- **e2e**: `WithBindMount` a host dir created **world-writable** (`UnixFileMode` 777, guarded off on Windows) so
  the non-root container can write to `/certs`; recreating it fresh at the start of each run keeps runs isolated
  (deletion works because the dir is world-writable).

## Risks / Trade-offs
- `COPY --chown=$APP_UID` relies on the base image's `APP_UID` env (1654 on .NET chiseled); if it does not expand,
  fall back to the literal uid. Verified at the e2e image build.
- The "no XML encryptor" DP warning (keys unencrypted at rest) remains — consistent with the unencrypted TLS
  private keys already stored in `/certs`; at-rest encryption is a separate follow-up.
- Reference compose changes the `certs` mount from a host bind to a named volume — a migration for operators.

## Migration Plan
- Operators using the old `./certs` bind mount should switch to the named volume (documented in
  `docs/deployment.md`).

## Open Questions
- e2e restart-persistence test (follow-up backlog item).
