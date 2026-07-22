## 1. HTTPS listener (AG-AT)

- [x] 1.1 Inject `DefaultCertificateProvider` into `KestrelTlsConfigurator`; in `ConfigureHttpsDefaults` set `ServerCertificate` = fallback (default) AND keep `ServerCertificateSelector` (per-SNI)
- [x] 1.2 Add `ASPNETCORE_HTTPS_PORTS=8443` to the `Dockerfile` (keep `ASPNETCORE_HTTP_PORTS=8080`)
- [x] 1.3 Map `443:8443` in `docker-compose.yml`

## 2. Tests & verification (AG-AT)

- [x] 2.1 Smoke test: `KestrelTlsConfigurator.Configure(new KestrelServerOptions())` runs without throwing (guards ctor/DI signature)
- [x] 2.2 Confirm the solution builds and the full test suite stays green via the Nuke CLI (`./build.ps1 Test`)
- [x] 2.3 Note runtime verification (WSL): container serves HTTPS on 8443 with the fallback cert for unknown hosts

## 3. Documentation (AG-AT)

- [x] 3.1 Update `docs/deployment.md` / `docs/tls-acme.md` to state HTTPS now listens on 8443 (mapped to 443)
