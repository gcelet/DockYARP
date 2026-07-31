## Why
Per-SNI TLS **policy** (protocols + cipher suites), not just the certificate, cannot be expressed through
`ConfigureHttpsDefaults`: only the Kestrel handshake callbacks (`ServerOptionsSelectionCallback` /
`TlsHandshakeCallbackOptions.OnConnection`) receive the SNI host *and* let the app build a full
`SslServerAuthenticationOptions` per connection — and those callbacks **bypass** `ConfigureHttpsDefaults` and the
default certificate. This change moves TLS termination onto that callback **without changing any behavior**, so
the per-vhost `SSL_POLICY` feature (`add-per-vhost-ssl-policy`) can layer on cleanly and the risky hot-path
refactor is de-risked on its own by the existing e2e regression.

## What Changes
- The App **owns its data-plane endpoints**: it binds HTTP (`Server:HttpPort`, default `80`) and HTTPS
  (`Server:HttpsPort`, default `443`) explicitly in `ConfigureKestrel`, instead of relying on host-injected
  `ASPNETCORE_URLS`/`ASPNETCORE_HTTPS_PORTS`. Configuring endpoints in code makes Kestrel ignore those URLs (a
  benign "Overriding address(es)…" warning), so there is no double-bind.
- The HTTPS endpoint is wired via `UseHttps(TlsHandshakeCallbackOptions)`. A new `SniTlsHandshakeCallback`
  assembles, per connection and keyed by `ClientHelloInfo.ServerName`, the same certificate (existing
  `SniCertificateSelector`), TLS protocol floor + cipher policy (existing global `SslPolicyPresets`/
  `TlsHardening`), and mTLS policy (existing `ClientCertificateValidator`) as today.
- No observable behavior changes: same certificates, TLS versions, ciphers, HTTP protocols, mTLS, and the HTTP
  endpoint stays plaintext HTTP/1.1. `KestrelTlsConfigurator`'s `ConfigureHttpsDefaults` path is replaced by the
  explicit endpoint + callback.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: TLS session properties are assembled per connection from the SNI host (defaulting to the global
  posture), rather than only the certificate being SNI-selected under a single global HTTPS default.

## Impact
- **Code**: `DockYarp.Tls` — new `SniTlsHandshakeCallback` + `TlsHandshakeCallbackOptions` wiring; rework
  `KestrelTlsConfigurator` (drop `ConfigureHttpsDefaults`, expose the endpoint binder). `DockYarp.App`
  (`Program.cs`) — explicit `ListenAnyIP` binding from `Server:HttpPort`/`Server:HttpsPort`.
- **Config**: new `Server:HttpPort` (80) / `Server:HttpsPort` (443).
- **Tests (unit)**: the callback assembles the expected `SslServerAuthenticationOptions` for a host (cert,
  protocols, cipher policy, mTLS required/validation) from the global posture; HTTP endpoint stays HTTP/1.1.
- **Runtime / e2e**: **behavior-preserving** → validated by the **existing** Aspire e2e regression (TLS
  handshake, SNI, mTLS, step-ca). No new e2e item. The e2e AppHost passes `Server__HttpPort=8080` /
  `Server__HttpsPort=8443` to match its endpoint `targetPort`s.
- **Deployment**: default `80`/`443` (or explicit `Server__*`). Update `docs/` if user-facing.
- **Owning agent**: AG-AT (with AG-DEP for the endpoint/deploy wiring). Enables `add-per-vhost-ssl-policy`;
  resolves `add-tls-handshake-callback`.
