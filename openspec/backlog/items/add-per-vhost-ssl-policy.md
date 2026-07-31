---
id: add-per-vhost-ssl-policy
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: SSL_POLICY (per-vhost override; DUAL global default + per-container)
provenance: 2026-07-31 env-var compat pass (parity re-analysis)
depends-on: add-tls-handshake-callback
---

## Why
nginx-proxy's `SSL_POLICY` is **DUAL**: a global default on the proxy container **and** a per-vhost override set
as an env var/label on the backend container. DockYarp implements only the **global** posture (`Tls:SslPolicy`);
a container that sets `-e SSL_POLICY=Mozilla-Modern` on itself is ignored. This closes the per-container
env-var-compatibility gap for `SSL_POLICY`, on top of the per-SNI TLS assembly point introduced by
`add-tls-handshake-callback`.

## nginx-proxy behavior
- `SSL_POLICY` (default `Mozilla-Intermediate`) selects a protocol/cipher profile. Read per-vhost via
  `groupByKeys $vhost_containers "Env.SSL_POLICY" | first | default $globals.config.ssl_policy`
  (`nginx.tmpl` 813; global 27/564). Mozilla profiles + AWS-ELB policies.

## DockYarp today
- Global only: `TlsOptions.SslPolicy` → `SslPolicyPresets.Resolve` → applied once in `KestrelTlsConfigurator`.
  `SSL_POLICY` is **not** a recognized container key (`DockerLabels` has no entry; `LabelParser` never reads it),
  so a per-container value has no effect.
- After `add-tls-handshake-callback`: TLS options are assembled **per connection** from the SNI host, so a
  per-host policy can be injected at that point.

## Proposed change (sketch)
- Recognize the container key: `DockerLabels.SslPolicy = "SSL_POLICY"`; parse into
  `ContainerLabelConfig.SslPolicy` (the raw preset name, validated against the known presets; unknown → warn +
  ignore, like other `Has*` diagnostics).
- Flow the per-host preset into the route config store next to `CERT_NAME` (the same store
  `SniCertificateSelector` already consults per host).
- Add a `HostSslPolicyResolver` that, given the SNI host, returns the effective `SslPolicyResolution` — the
  per-host preset when set, else the global `Tls:SslPolicy` — reusing `SslPolicyPresets.Resolve`.
- In `SniTlsHandshakeCallback`, use that resolution for `EnabledSslProtocols` + `CipherSuitesPolicy`
  (Linux/macOS) instead of the single global resolution.
- Precedence stays env-wins-over-label (already handled by `LabelParser.EffectiveConfig`). Global remains the
  default when no per-host value is present.

## Acceptance criteria (→ scenarios)
- **WHEN** a container sets `SSL_POLICY=Mozilla-Modern` (env or label) **THEN** the handshake for its host
  negotiates only TLS 1.3, while a host without the override keeps the global policy.
- **WHEN** the per-container `SSL_POLICY` is unknown **THEN** it is ignored with a diagnostic and the global
  policy applies (no failed startup).
- **WHEN** `SSL_POLICY` is set as both env and label **THEN** the env value wins.

## Notes / risks / references
- Depends on [`add-tls-handshake-callback`](add-tls-handshake-callback.md) (per-connection TLS assembly point).
- Parsing/resolution is pure + unit-testable (no Docker). Live per-host negotiation is validated by
  [`e2e-ssl-policy-negotiation`](e2e-ssl-policy-negotiation.md) — extend it with a per-vhost case (two hosts,
  different policies, one Kestrel instance).
- Cipher enforcement is Linux/macOS only (as today); the per-host protocol floor works cross-platform.
- Parity: this splits the current `SSL_POLICY` ✅ (global) into global ✅ + per-vhost override (⛔→✅ on archive).
