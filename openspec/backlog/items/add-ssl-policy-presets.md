---
id: add-ssl-policy-presets
capability: tls-acme
agent: AG-AT
tier: C-doc
priority: low
status: backlog
nginx-proxy: SSL_POLICY
provenance: this parity pass (matrix: SSL_POLICY ⛔)
---

## Why
nginx-proxy exposes named cipher/protocol policies via `SSL_POLICY` (Mozilla-Intermediate/Modern/Old + AWS ELB
sets) so operators pick a posture without hand-listing ciphers. DockYarp requires raw cipher/protocol config,
which is error-prone.

## nginx-proxy behavior
- `SSL_POLICY` (per proxy or per container) selects a predefined protocol+cipher suite policy; default
  `Mozilla-Intermediate`.

## DockYarp today
`Tls:MinimumTlsVersion`, `HttpProtocols`, and a raw `CipherSuites` allow-list (Linux/macOS only) in
`src/DockYarp.Tls/TlsOptions.cs` + `TlsHardening.cs`. No named presets.

## Proposed change (sketch)
Add a `Tls:SslPolicy` option mapping named presets (at least Mozilla Intermediate/Modern/Old) to concrete
min-version + cipher lists, layered over the existing hardening. Presets set defaults; explicit
`CipherSuites`/`MinimumTlsVersion` still override.

## Acceptance criteria (→ scenarios)
- **WHEN** `Tls:SslPolicy=Mozilla-Modern` **THEN** the HTTPS endpoint negotiates only the modern protocol/
  cipher set.
- **WHEN** a preset and an explicit cipher list are both set **THEN** the explicit list wins.

## Notes / risks / references
- Cipher enforcement is platform-gated today (Linux/macOS); document the Windows limitation for presets too.
