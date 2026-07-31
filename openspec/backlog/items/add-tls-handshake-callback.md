---
id: add-tls-handshake-callback
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: (enabler — no direct token; unblocks per-SNI TLS policy)
provenance: split from add-per-vhost-ssl-policy, 2026-07-31 env-var compat pass
---

## Why
Per-SNI TLS *policy* (protocols + cipher suites), not just the certificate, cannot be expressed through
`ConfigureHttpsDefaults`: the only Kestrel hooks that both receive the SNI host **and** let the app build a full
`SslServerAuthenticationOptions` per connection are the handshake callbacks (`ServerOptionsSelectionCallback` /
`TlsHandshakeCallbackOptions.OnConnection`), and those **bypass** `ConfigureHttpsDefaults` and the default
certificate. This change migrates the TLS termination to that callback **without changing any behavior**, so the
per-vhost feature (`add-per-vhost-ssl-policy`) can layer on cleanly and the risky hot-path refactor is de-risked
on its own by the existing e2e regression.

## nginx-proxy behavior
- N/A. This is an internal enabler. nginx assembles per-server TLS settings from its generated config; DockYarp
  needs an equivalent per-connection assembly point.

## DockYarp today
- `KestrelTlsConfigurator` (`src/DockYarp.Tls/KestrelTlsConfigurator.cs`) configures a single global posture via
  `ConfigureHttpsDefaults`: `SslProtocols`, `ServerCertificate` (fallback default), `ServerCertificateSelector`
  (per-SNI cert via `SniCertificateSelector`), `OnAuthenticate` (global `CipherSuitesPolicy`, Linux/macOS), and
  mTLS (`ClientCertificateMode` + `ClientCertificateValidation`).
- Endpoints come from ports-only host config (`ASPNETCORE_HTTPS_PORTS`/`ASPNETCORE_HTTP_PORTS`); the HTTP port is
  detected to keep the plaintext endpoint HTTP/1.1 (ACME challenges + redirects).

## Proposed change (sketch)
- **App owns the data-plane endpoints.** Because both handshake-callback APIs are attached via a `UseHttps`
  overload on an explicit `ListenOptions` (and bypass `ConfigureHttpsDefaults`), the App must bind its
  HTTP/HTTPS endpoints in code instead of relying on host-injected `ASPNETCORE_URLS`/`ASPNETCORE_HTTPS_PORTS`.
  - Add config `Server:HttpPort` (default `80`) and `Server:HttpsPort` (default `443`); bind via
    `ConfigureKestrel`: `ListenAnyIP(httpPort, o => o.Protocols = Http1)` (plaintext, ACME + redirects) and
    `ListenAnyIP(httpsPort, o => o.UseHttps(tlsCallbackOptions))`.
  - Configuring endpoints in code makes Kestrel **ignore** `ASPNETCORE_URLS` (logs a benign "Overriding
    address(es)…" warning), so there is **no double-bind** with host-injected URLs.
- Introduce a `SniTlsHandshakeCallback` (wraps `TlsHandshakeCallbackOptions.OnConnection`) that, per connection,
  reads `context.ClientHelloInfo.ServerName` and builds `SslServerAuthenticationOptions`:
  - server certificate via the existing `SniCertificateSelector.Select(host)`;
  - `EnabledSslProtocols` + optional `CipherSuitesPolicy` from the current **global** resolution
    (`SslPolicyPresets.Resolve` + `TlsHardening`) — unchanged behavior for now;
  - mTLS: `ClientCertificateRequired` / `RemoteCertificateValidationCallback` mirroring the current
    `ClientCertificateValidator` wiring.
- Keep the low-allocation posture on the hot path (cache the immutable global pieces; only the per-host
  cert/policy lookup runs per handshake).

## Blast radius (beyond DockYarp.Tls)
- `DockYarp.App` (`Program.cs`): explicit endpoint binding from `Server:HttpPort/HttpsPort`.
- `tests/DockYarp.E2E.AppHost`: the DockYarp resource keeps `WithHttpEndpoint(targetPort:8080)` /
  `WithHttpsEndpoint(targetPort:8443)`, but must now pass `Server__HttpPort=8080` / `Server__HttpsPort=8443`
  so the App binds the ports Aspire's endpoints target.
- Deployment/compose: default `80`/`443` (or explicit `Server__*` env). Document in `docs/` if user-facing.

## Acceptance criteria (→ scenarios)
- **WHEN** a client connects over HTTPS with SNI `app.local` **THEN** it receives the same certificate, TLS
  version, ciphers, and mTLS behavior as before this change (pure refactor).
- **WHEN** mutual TLS is enabled **THEN** a client cert chaining to the configured CA is accepted and an
  untrusted one is rejected, exactly as before.
- **WHEN** a plaintext request hits the HTTP endpoint **THEN** it is served HTTP/1.1 (ACME challenge / redirect),
  never TLS.

## Notes / risks / references
- Hot path + mTLS refactor; **behavior-preserving** by design → validated by the **existing** Aspire e2e
  regression (TLS handshake, SNI, mTLS), runnable in the Docker/WSL session. No new e2e needed for this item.
- API refs (validated via microsoft-docs, aspnetcore-10.0): `TlsHandshakeCallbackOptions`,
  `ServerOptionsSelectionCallback`, `ListenOptionsHttpsExtensions.UseHttps`. Both callbacks bypass
  `ConfigureHttpsDefaults` and the default certificate.
- Blocks: [`add-per-vhost-ssl-policy`](add-per-vhost-ssl-policy.md). Adjacent: `add-external-port-config`
  (explicit endpoints also make external-port overrides tractable).
