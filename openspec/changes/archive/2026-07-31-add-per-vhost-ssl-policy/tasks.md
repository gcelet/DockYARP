## 1. Recognize the key (AG-DD)
- [x] 1.1 `DockerLabels.SslPolicy = "SSL_POLICY"`
- [x] 1.2 `ContainerLabelConfig.SslPolicy` (string?)
- [x] 1.3 `LabelParser.TryParse` + `ParseCommon`: `SslPolicy = GetOrNull(labels, DockerLabels.SslPolicy)`
      (env-over-label inherited from `EffectiveConfig`)

## 2. Carry it per host (AG-DD / AG-AT)
- [x] 2.1 `HostTlsMetadata.SslPolicy` (string?)
- [x] 2.2 `ContainerMapper`: set `SslPolicy = first.SslPolicy` / `common.SslPolicy` in both `HostTlsMetadata`
      blocks (creation condition unchanged — honored for LETSENCRYPT_HOST / CERT_NAME hosts)

## 3. Resolve + apply per connection (AG-AT)
- [x] 3.1 `SslPolicyPresets.KnownPresetNames` (expose the preset name set)
- [x] 3.2 `HostSslPolicyResolver.Resolve(snapshot, host)` — per-host `SSL_POLICY` string (pure, like
      `CertificateNameResolver`)
- [x] 3.3 `SniTlsHandshakeCallback`: inject `IRouteConfigStore` + `ILogger`; precompute global + per-preset
      prepared policies; per connection apply the host's preset (protocols + cipher policy), else global;
      warn-once (`TlsLog`) on an unknown per-host value
- [x] 3.4 Register the logger dependency (resolved via DI; registration unchanged)

## 4. Tests (AG-AT / AG-DD)
- [x] 4.1 `LabelParser`: `SSL_POLICY` parsed, env wins over a same-named label
- [x] 4.2 `ContainerMapper`: a certified host with `SSL_POLICY` carries it into `Tls.SslPolicy`
- [x] 4.3 `HostSslPolicyResolver`: matches the policy by host; null when absent
- [x] 4.4 `SniTlsHandshakeCallback`: `Mozilla-Modern` host → TLS 1.3 only; host without → global posture;
      unknown value → global posture

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker) — 308 tests, 0 failures
- [ ] 5.2 e2e (batched, WSL): extend `e2e-ssl-policy-negotiation` with a per-vhost two-host case → flips the
      parity row to ✅
