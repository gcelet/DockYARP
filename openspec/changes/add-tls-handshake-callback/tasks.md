## 1. Config + endpoint ownership (AG-DEP)
- [x] 1.1 Add `ServerEndpointOptions` (`Server:HttpPort` default `8080` / `Server:HttpsPort` default `8443`,
      matching the non-root container convention); bind in `Program.cs`, register in DI
- [x] 1.2 Bind endpoints in code from `KestrelTlsConfigurator`: `ListenAnyIP(httpPort, Http1)` +
      `ListenAnyIP(httpsPort, UseHttps(tlsCallbackOptions))`; `ASPNETCORE_URLS`/`*_PORTS` are overridden (no
      double-bind)
- [x] 1.3 Local dev coherence: `appsettings.json` documents `Server:*`; `launchSettings.json` `applicationUrl`
      points at 8080/8443 (the actually-bound ports)

## 2. Per-connection TLS assembly (AG-AT)
- [x] 2.1 `SniTlsHandshakeCallback`: `TlsHandshakeCallbackOptions.OnConnection` → build
      `SslServerAuthenticationOptions` from `ClientHelloInfo.ServerName` (cert via `SniCertificateSelector`;
      protocols + cipher policy from the captured **global** `SslPolicyResolution`; mTLS request + CA validation)
- [x] 2.2 Resolve the global `SslPolicyResolution` once at startup and capture it; keep cipher policy
      Linux/macOS-guarded; keep the mTLS validation delegate a single cached instance over the captured validator
- [x] 2.3 Rework `KestrelTlsConfigurator`: drop `ConfigureHttpsDefaults`; keep the "HTTP port stays HTTP/1.1"
      guarantee via the explicit HTTP endpoint; HTTPS keeps configured `HttpProtocols`
- [x] 2.4 Register `SniTlsHandshakeCallback` (+ `ServerEndpointOptions` default) in `TlsServiceCollectionExtensions`

## 3. Deploy wiring (AG-DEP)
- [x] 3.1 `Dockerfile`: replace `ASPNETCORE_HTTP_PORTS`/`ASPNETCORE_HTTPS_PORTS` with `Server__HttpPort=8080` /
      `Server__HttpsPort=8443` (the code defaults match, so the Aspire e2e AppHost — image `dockyarp:local` on
      targetPort 8080/8443 — needs **no** change)
- [x] 3.2 Note the data-plane ports (`Server__*`, default 8080/8443) in `docs/deployment.md`

## 4. Tests (AG-AT)
- [x] 4.1 Unit: the callback yields the expected `SslServerAuthenticationOptions` for a host — correct cert,
      `EnabledSslProtocols` for the global TLS floor, ALPN, mTLS required + validation delegate present when a CA
      is configured (and absent otherwise); TLS 1.3 floor narrows protocols
- [x] 4.2 Unit: `TlsHardening.ToApplicationProtocols` mapping (h2 preferred, defaults to h2+http/1.1)
- [~] 4.3 Endpoint binding + `UseHttps(callback)` requires the full Kestrel DI graph (e.g. `KestrelMetrics`), so
      "HTTP pinned to Http1 / HTTPS keeps HttpProtocols" is validated by the e2e regression, not a unit test

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker) — 302 tests, 0 failures
- [ ] 5.2 Existing Aspire e2e regression green from WSL (`./build.sh E2E`) — SNI, mTLS, ACME challenge unchanged
