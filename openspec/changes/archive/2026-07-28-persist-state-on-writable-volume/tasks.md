## 1. Writable volume (AG-DEP)
- [x] 1.1 Dockerfile: create `/certs` owned by `$APP_UID` (build-stage seed dir + `COPY --chown`) and set
      `ENV Tls__CertificateDirectory=/certs`
- [x] 1.2 Program.cs: persist Data Protection keys under `<CertificateDirectory>/dataprotection-keys`
- [x] 1.3 docker-compose: use a named volume for `/certs` (inherits app ownership) instead of a host bind mount

## 2. e2e (AG-DEP)
- [x] 2.1 `E2EPaths`: add `CertsDirectory`; `TlsHarness`: recreate it world-writable (`UnixFileMode` 777, off on
      Windows) each run
- [x] 2.2 AppHost: bind-mount `CertsDirectory` at `/certs` on the DockYarp container

## 3. Docs (AG-DEP)
- [x] 3.1 `docs/deployment.md`: explain the non-root-writable volume, the named volume, and DP key persistence

## 4. Verify (AG-DEP)
- [x] 4.1 Build green; to confirm at the next `E2E` run that certs still provision (non-root writes `/certs`) and
      the ephemeral-DP-keys warning is gone
