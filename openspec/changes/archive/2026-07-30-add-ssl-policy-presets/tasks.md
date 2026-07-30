## 1. Option + presets (AG-AT)
- [x] 1.1 `TlsOptions`: add `SslPolicy` (`string?`, default null)
- [x] 1.2 New pure `SslPolicyPresets.Resolve(policy, configuredVersion, configuredCiphers)` → effective
      `(MinimumTlsVersion, CipherSuites)`; Mozilla `Modern`/`Intermediate`/`Old`; explicit ciphers win; unknown
      or unset → configured values

## 2. Apply (AG-AT)
- [x] 2.1 `KestrelTlsConfigurator`: resolve the effective version + ciphers via `SslPolicyPresets` and use them
      for `SslProtocols` and the cipher policy

## 3. Split runtime validation (AG-AT)
- [x] 3.1 New backlog item `e2e-ssl-policy-negotiation` (live handshake negotiates only the preset's suites; Linux)

## 4. Tests (AG-AT)
- [x] 4.1 `SslPolicyPresets`: `Modern` → TLS 1.3 + TLS 1.3 suites; `Intermediate` → TLS 1.2 + expected list;
      explicit cipher list overrides the preset; unknown/unset policy returns the configured values

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green
