# Design — add-ssl-policy-presets

## Context
`KestrelTlsConfigurator` applies TLS hardening from `TlsOptions`: `MinimumTlsVersion` → `SslProtocols` and an
optional raw `CipherSuites` allow-list (via pure `TlsHardening` mappings; ciphers apply on Linux/macOS only).
There is no named-preset shortcut, so operators must hand-list ciphers.

## Decisions

### 1. `Tls:SslPolicy` names a preset
Add `TlsOptions.SslPolicy` (string). Recognized values map to a Mozilla posture:
- **Mozilla-Modern** → TLS 1.3 only, the three TLS 1.3 suites.
- **Mozilla-Intermediate** → TLS 1.2 minimum, TLS 1.3 suites + ECDHE/DHE AES-GCM and CHACHA20 suites.
- **Mozilla-Old** → TLS 1.2 minimum (DockYarp floors at 1.2; it does not enable TLS 1.0/1.1), intermediate
  suites plus common ECDHE CBC suites.

Matching is case-insensitive on the hyphenated names.

### 2. A pure `SslPolicyPresets.Resolve`
`Resolve(policy, configuredVersion, configuredCiphers)` returns the effective `(MinimumTlsVersion, CipherSuites)`:
- unknown or unset policy → `(configuredVersion, configuredCiphers ?? [])` (unchanged behavior);
- a recognized preset → `(preset.version, configuredCiphers non-empty ? configuredCiphers : preset.ciphers)`.

So a preset sets the minimum version and default ciphers, while an explicit `CipherSuites` list still wins. The
result is expressed as cipher **names** and fed through the existing `TlsHardening.ParseCipherSuites`, which
skips names unknown to the platform — so the presets stay robust across runtimes.

### 3. Apply in the configurator
`KestrelTlsConfigurator` resolves the effective values once and uses them for `SslProtocols` and the cipher
policy, replacing the direct reads of `MinimumTlsVersion`/`CipherSuites`.

## Testability
- **Unit**: `SslPolicyPresets.Resolve` — each preset's version + cipher list, explicit-ciphers-win, and the
  unknown/unset fallback are deterministic and covered.
- **Not unit-testable → E2E**: that the live endpoint negotiates only the preset's suites is a handshake
  behavior, enforced only on Linux/macOS. Split to `e2e-ssl-policy-negotiation` (a TLS handshake against a
  Linux-hosted endpoint), consistent with the testing pyramid.

## Risks
- A typo in `SslPolicy` silently falls back to the configured values (no preset applied); documented. Cipher
  enforcement remains platform-gated (Linux/macOS), so on Windows a preset sets the minimum version but not the
  cipher restriction.
