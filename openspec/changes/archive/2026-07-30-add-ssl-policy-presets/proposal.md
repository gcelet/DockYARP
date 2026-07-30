## Why
nginx-proxy exposes named cipher/protocol policies via `SSL_POLICY` (Mozilla Modern/Intermediate/Old) so
operators pick a security posture without hand-listing ciphers. DockYarp requires a raw cipher/protocol
configuration, which is error-prone.

## What Changes
- Add a `Tls:SslPolicy` option mapping a named preset (Mozilla `Modern`, `Intermediate`, `Old`) to a concrete
  minimum TLS version + cipher-suite list, layered over the existing hardening.
- An explicit `Tls:CipherSuites` list overrides the preset's ciphers; an unknown or unset policy leaves the
  configured values unchanged (current behavior).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: TLS hardening accepts a named `SSL_POLICY` preset that sets the minimum version and ciphers.

## Impact
- **Code**: `DockYarp.Tls` — `TlsOptions.SslPolicy`, a pure `SslPolicyPresets.Resolve` (preset → version +
  ciphers, explicit ciphers win), and `KestrelTlsConfigurator` applying the resolved values.
- **Tests (unit)**: `SslPolicyPresets` — each preset maps to the expected version + cipher list; an explicit
  cipher list wins; an unknown/unset policy falls back to the configured values.
- **Runtime / not unit-testable**: that the running HTTPS endpoint actually *negotiates only* the preset's
  suites is a live-handshake behavior and, like the existing cipher allow-list, is enforced only on
  Linux/macOS. This is split to a new backlog item `e2e-ssl-policy-negotiation` (E2E TLS handshake on Linux).
  The unit tests cover the configuration mapping only.
- **Owning agent**: AG-AT. Resolves `add-ssl-policy-presets`.
