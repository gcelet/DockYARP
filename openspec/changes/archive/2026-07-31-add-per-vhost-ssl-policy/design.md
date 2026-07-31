# Design — add-per-vhost-ssl-policy

## Goal
Honor a per-container `SSL_POLICY` (env/label) as a per-host override of the global TLS posture, using the
per-connection assembly point from `add-tls-handshake-callback`.

## Data path (mirrors CERT_NAME)
`SSL_POLICY` follows the exact channel already used by `CERT_NAME`:

```
DockerLabels.SslPolicy ("SSL_POLICY")
  → LabelParser.TryParse / ParseCommon: SslPolicy = GetOrNull(config, SSL_POLICY)   (env wins via EffectiveConfig)
  → ContainerLabelConfig.SslPolicy
  → ContainerMapper: HostTlsMetadata { SslPolicy = first.SslPolicy }                 (both classic + multiports)
  → RouteRule.Tls.SslPolicy
  → HostSslPolicyResolver.Resolve(snapshot, host)                                     (Tls, pure, like CertificateNameResolver)
  → SniTlsHandshakeCallback: per-connection protocol floor + cipher policy
```

## Cross-layer constraint
`DockYarp.Docker` does **not** reference `DockYarp.Tls`, so `LabelParser` cannot validate the value against the
preset list (which lives in `SslPolicyPresets`). Therefore:
- The Docker layer only **reads** the raw string (no validation, no warning).
- Recognition + the "unknown preset" **diagnostic live in the Tls layer**, at the point of application —
  `SniTlsHandshakeCallback` warns once per unknown value (mirroring `SniCertificateSelector`'s warn-once for a
  missing `CERT_NAME`), then falls back to the global posture.

## SniTlsHandshakeCallback
- Inject `IRouteConfigStore` (the callback already sits next to `SniCertificateSelector`, which reads it) and an
  `ILogger`.
- **Precompute** prepared policies once (no per-connection cipher parsing):
  - `globalPolicy` = `Prepare(SslPolicyPresets.Resolve(options.SslPolicy, options.MinimumTlsVersion, options.CipherSuites))`.
  - `presetPolicies[name]` = `Prepare(SslPolicyPresets.Resolve(name, Tls12, null))` for each
    `SslPolicyPresets.KnownPresetNames` (pure preset: the global explicit ciphers do **not** bleed into a
    per-host preset — a per-vhost policy fully replaces the posture for that vhost, matching nginx-proxy).
  - `Prepare(resolution)` → `(SslProtocols, CipherSuitesPolicy?)`; the cipher policy stays Linux/macOS-guarded.
- Per connection (`ResolvePolicy(host)`): resolve the host's `SSL_POLICY` string; if it names a known preset use
  it, else warn-once (unknown) and use `globalPolicy`; an absent value uses `globalPolicy`. Apply
  `EnabledSslProtocols` + optional `CipherSuitesPolicy`. ALPN and mTLS stay as before (not policy-dependent).
- Only a dictionary lookup runs per handshake; the single `SslServerAuthenticationOptions` allocation is
  unavoidable (the callback API is per-connection).

## Scope decision (consistent with the existing model)
`HostTlsMetadata` is created only for hosts that declare `LETSENCRYPT_HOST` or `CERT_NAME` — and that is already
where per-host `HSTS`/`HTTPS_METHOD` are carried. Per-host `SSL_POLICY` follows the same rule: it is honored for
those TLS-configured hosts. It deliberately does **not** widen the metadata-creation condition, because
`TlsDomains.Desired` treats any route with a `CertificateHost` (and no `CERT_NAME`) as an ACME desire — widening
it would provision certificates for policy-only hosts. A host with a provided cert but no
`LETSENCRYPT_HOST`/`CERT_NAME`, or one on the self-signed fallback, keeps the global policy. (A broader "per-host
TLS attributes for every TLS host" change is out of scope here.)

## Precedence
Env-over-label is inherited: `LabelParser` reads from `EffectiveConfig` (env overlaid on labels), so
`SSL_POLICY` set as an env var already wins over a same-named label. No new precedence logic.

## Tests
- `LabelParser`: `SSL_POLICY` parsed from env and from label; env wins.
- `ContainerMapper`: a `LETSENCRYPT_HOST` (or `CERT_NAME`) host with `SSL_POLICY` carries it into `Tls.SslPolicy`.
- `HostSslPolicyResolver`: returns the policy for a matching host, null otherwise.
- `SniTlsHandshakeCallback`: host with `Mozilla-Modern` → TLS 1.3 only; host without → global; unknown → global.
- e2e (later): extend `e2e-ssl-policy-negotiation` with two hosts on one instance negotiating different policies.
