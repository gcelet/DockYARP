## 1. Recognize the keys (AG-DD)
- [x] 1.1 `DockerLabels.EnableHttpOnMissingCert = "ENABLE_HTTP_ON_MISSING_CERT"`,
      `DockerLabels.TrustDefaultCert = "TRUST_DEFAULT_CERT"`,
      `DockerLabels.NginxTrustDefaultCert = "com.github.nginx-proxy.nginx-proxy.trust-default-cert"`
- [x] 1.2 `ContainerLabelConfig.EnableHttpOnMissingCert` / `.TrustDefaultCert` (bool?)
- [x] 1.3 `LabelParser`: `ParseBool` helper; read both in `TryParse` + `ParseCommon` (trust-default-cert: plain
      key first, then the namespaced alias)

## 2. Carry it per host (AG-DD / AG-SEC)
- [x] 2.1 `HostTlsMetadata.EnableHttpOnMissingCert` / `.TrustDefaultCert` (bool?)
- [x] 2.2 `ContainerMapper`: set both in the classic + multiports `HostTlsMetadata` blocks

## 3. Apply it (AG-SEC)
- [x] 3.1 `HttpsRedirectionMiddleware`: `tls.TrustDefaultCert ?? options.TrustDefaultCert` and
      `tls.EnableHttpOnMissingCert ?? options.EnableHttpOnMissingCert`

## 4. Tests (AG-SEC / AG-DD)
- [x] 4.1 `LabelParser`: `ENABLE_HTTP_ON_MISSING_CERT`/`TRUST_DEFAULT_CERT` parsed (incl. the namespaced alias)
- [x] 4.2 `ContainerMapper`: a certified host carries both into `Tls`
- [x] 4.3 `HttpsRedirectionMiddleware`: per-host `TrustDefaultCert=false` → 500 with a permissive global;
      per-host `EnableHttpOnMissingCert=false` → forced redirect with a permissive global

## 5. Verify (AG-SEC)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker) — 321 tests, 0 failures
