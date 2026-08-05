---
id: add-ssl-policy-elb-presets
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: low
status: backlog
nginx-proxy: SSL_POLICY (AWS ELB security policies)
provenance: 2026-08-05 parity re-comparison
---

## Why
nginx-proxy's `SSL_POLICY` accepts, besides `Mozilla-Modern`/`Intermediate`/`Old`, roughly **20 AWS ELB
security policies** (e.g. `ELBSecurityPolicy-TLS13-1-2-2021-06`, `ELBSecurityPolicy-FS-1-2-Res-2020-10`).
DockYarp recognizes only the three Mozilla presets; an ELB policy name is unrecognized and falls back to the
global default.

## nginx-proxy behavior
- `SSL_POLICY` maps a policy name to a concrete `ssl_protocols` + `ssl_ciphers` (+ `ssl_conf_command`,
  `ssl_prefer_server_ciphers`). The ELB names correspond to AWS's published protocol/cipher tables.

## DockYarp today
- `src/DockYarp.Tls/SslPolicyPresets.cs` defines `Mozilla-Modern`/`Intermediate`/`Old` only (TLS version +
  cipher suites). Applied globally (`Tls:SslPolicy`) and per-host (`SSL_POLICY`).

## Proposed change (sketch)
- Add the AWS ELB security-policy presets to `SslPolicyPresets` (name → min TLS version + cipher-suite set),
  from AWS's published policy tables.

## Acceptance criteria (→ scenarios)
- **WHEN** `SSL_POLICY=ELBSecurityPolicy-…` is set (global or per-host) **THEN** the mapped TLS version + ciphers
  are applied.
- **WHEN** the name is unrecognized **THEN** the global default is used, unchanged.

## Notes / risks / references
- **Niche / low value** — most users use the Mozilla presets. Mostly a data-entry task (long cipher lists).
- **Risk**: DockYarp's cipher-suite configuration is applied on **Linux/macOS only** (SChannel picks its own on
  Windows), and OpenSSL cipher names (nginx/AWS docs) don't map 1:1 to .NET `TlsCipherSuite` — the mapping will
  be approximate on some platforms. Consider whether to map only the TLS-version floor and accept default
  ciphers, or the full suite list.
- Sibling (done): `add-ssl-policy-presets` / per-vhost `add-per-vhost-ssl-policy` (the Mozilla presets).
