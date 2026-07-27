---
id: finish-http3
capability: deployment
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: ENABLE_HTTP3 / com.github.nginx-proxy.nginx-proxy.http3.enable
provenance: this parity pass (matrix: HTTP/3 ⚠️ config toggle only)
---

## Why
nginx-proxy supports HTTP/3 (QUIC) via `ENABLE_HTTP3`. DockYarp exposes an HTTP/3 config toggle but it is not
wired to a working QUIC listener (needs MsQuic) and is runtime-unvalidated.

## nginx-proxy behavior
- `ENABLE_HTTP3` (proxy-wide) / `...http3.enable` (per container), off by default; requires publishing
  `443/udp`; advertises `Alt-Svc`.

## DockYarp today
`Tls`/`HttpProtocols` config can request HTTP/3, but Kestrel needs MsQuic and a UDP endpoint; the path is a
config toggle only (matrix ⚠️). See `src/DockYarp.Tls/TlsOptions.cs` and the Kestrel listener wiring in
`src/DockYarp.App`.

## Proposed change (sketch)
Enable Kestrel HTTP/3 on the HTTPS endpoint (`HttpProtocols.Http1AndHttp2AndHttp3`), ensure the image/runtime
carries MsQuic, expose UDP/443, and emit `Alt-Svc`. Gate behind config; validate in a Docker-capable e2e run.

## Acceptance criteria (→ scenarios)
- **WHEN** HTTP/3 is enabled and MsQuic is available **THEN** the HTTPS endpoint negotiates HTTP/3 and sends an
  `Alt-Svc` header.
- **WHEN** MsQuic is unavailable **THEN** startup logs a clear warning and falls back to HTTP/1.1+2 without
  crashing.

## Notes / risks / references
- Chiseled image + MsQuic availability is the main risk; document the runtime requirement.
- Requires a Docker-capable session to validate (like the TLS e2e).
