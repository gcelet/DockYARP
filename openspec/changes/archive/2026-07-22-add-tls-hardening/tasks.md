## 1. TLS hardening (AG-AT)

- [x] 1.1 Add `TlsVersion` enum and `TlsOptions.MinimumTlsVersion`/`HttpProtocols`/`CipherSuites`
- [x] 1.2 Add pure `TlsHardening` helper: min version → `SslProtocols`, parse `HttpProtocols`, parse cipher names
- [x] 1.3 `KestrelTlsConfigurator` applies SslProtocols, protocols, and (Linux/macOS only) cipher policy

## 2. nohttps completion (AG-AT / AG-SEC)

- [x] 2.1 `TlsDomains.Desired` skips routes whose method is `NoHttps`
- [x] 2.2 `HttpsRedirectionMiddleware` refuses an HTTPS request (404) for a `NoHttps` route

## 3. HSTS (AG-DD / AG-RP / AG-SEC)

- [x] 3.1 Add `HostTlsMetadata.Hsts`; `HSTS` label parsed into `ContainerLabelConfig` and carried by the mapper
- [x] 3.2 `SecurityHeadersOptions.HstsPreload`; `SecurityHeadersMiddleware` is route-aware and applies preload + per-host override

## 4. Tests & docs

- [x] 4.1 `TlsHardening` tests: version→protocols, protocol parse, cipher parse (skip unknown)
- [x] 4.2 `TlsDomains` test: `nohttps` host excluded; middleware test: HTTPS refused for `nohttps`
- [x] 4.3 Header middleware tests: preload emitted; per-host `off` suppresses HSTS
- [x] 4.4 Parser/mapper tests: `HSTS` label parsed and carried
- [x] 4.5 Document TLS options + HSTS in `docs/tls-acme.md`, `docs/security-middleware.md`, `docs/labels-reference.md`
- [x] 4.6 Build + full test suite green via the Nuke CLI
