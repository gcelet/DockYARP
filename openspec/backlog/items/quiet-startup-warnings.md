---
id: quiet-startup-warnings
capability: deployment
agent: AG-AT
tier: C-doc
priority: low
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: e2e diagnostics log review, 2026-07-27 (dockyarp resource log)
---

## Why
DockYarp emits three benign-but-noisy warnings at startup that clutter logs and can worry operators. None
affects correctness, but a clean startup is more trustworthy.

## Observed behavior (e2e log — exact categories/event IDs)
```
warn: Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository[60]
      Storing keys in a directory '/home/app/.aspnet/DataProtection-Keys' that may not be persisted outside of the container...
warn: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[35]
      No XML encryptor configured. Key {..} may be persisted to storage in unencrypted form.
warn: Microsoft.AspNetCore.Server.Kestrel[64]
      HTTP/2 is not enabled for [::]:8080. The endpoint is configured to use HTTP/1.1 and HTTP/2, but TLS is not enabled...
```
The proxy holds no Data-Protection-protected payloads, and `8080` only serves ACME HTTP-01 challenges and
HTTP→HTTPS redirects over HTTP/1.1, so both are harmless.

## DockYarp today
- **Data Protection**: DockYarp does **not** call `AddDataProtection` explicitly (no reference in the codebase);
  it is registered transitively by ASP.NET, defaulting to ephemeral, unencrypted keys under
  `~/.aspnet/DataProtection-Keys`.
- **HTTP/2-without-TLS on 8080**: `src/DockYarp.Tls/KestrelTlsConfigurator.cs:58-59` sets
  `serverOptions.ConfigureEndpointDefaults(listen => listen.Protocols = TlsHardening.ParseHttpProtocols(options.HttpProtocols))`.
  `ConfigureEndpointDefaults` applies to **every** listener, so the plaintext `8080` endpoint also gets the
  default `Http1AndHttp2` (`TlsHardening.ParseHttpProtocols(null) == Http1AndHttp2`, see
  `tests/DockYarp.Tls.Tests/TlsHardeningTests.cs:29`); Kestrel then warns because HTTP/2 needs TLS/ALPN.

## Proposed change (sketch)
- **Data Protection**: since DockYarp needs no protected payloads, either persist keys to the mounted
  certificate directory so they survive restarts
  (`builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(<certsDir>/dataprotection-keys)).SetApplicationName("dockyarp")`),
  or relax/remove the dependency if it can be dropped. Note: the chiseled non-root image needs a **writable**
  target dir (the mounted `/certs` works; `/home/app/.aspnet` is ephemeral).
- **HTTP/2 warning**: apply `Http1AndHttp2` only to the TLS endpoint. The endpoints are **ports-only** (via
  `ASPNETCORE_HTTPS_PORTS`/`HTTP_PORTS` env — see the `KestrelTlsConfigurator` remarks), so per-endpoint
  protocol config isn't directly available; options:
  1. keep `ConfigureEndpointDefaults` at `Http1` and raise protocols to `Http1AndHttp2` inside
     `ConfigureHttpsDefaults`/the HTTPS listener only, **or**
  2. switch to explicit `ListenAnyIP(8080, o => o.Protocols = Http1)` + `ListenAnyIP(8443, https …)` instead of
     ports-only, **or**
  3. (cheapest) filter the `Microsoft.AspNetCore.Server.Kestrel` warning category.
  Prefer (1)/(2) so 8443 keeps HTTP/2; verify Kestrel exposes the protocol knob on the HTTPS defaults.

## Acceptance criteria (→ scenarios)
- **WHEN** DockYarp starts
- **THEN** the Data Protection ephemeral-keys/no-encryptor warnings and the HTTP/2-without-TLS warning are not
  emitted
- **WHEN** `8080` serves an ACME challenge or an HTTP→HTTPS redirect
- **THEN** it still works over HTTP/1.1
- **WHEN** an HTTPS client connects to `8443`
- **THEN** HTTP/2 is still negotiated (protocol capability preserved)

## Notes / risks / references
- Internal log-hygiene finding, **not an nginx-proxy parity gap** — no `parity.md` row.
- Confirm DockYarp genuinely needs no Data Protection before disabling; otherwise persist the keys.
- Touching Kestrel endpoint wiring interacts with the ports-only TLS setup and with `finish-http3` (HTTP/3 on
  8443) and `add-proxy-protocol`/custom-external-ports — keep those in mind if the listener config is reworked.
