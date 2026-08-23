## Why

The backlog has carried `add-dhparam-config` since the original nginx-proxy parity pass: nginx-proxy ships
`DHPARAM_BITS`/`DHPARAM_SKIP`/per-vhost `<host>.dhparam.pem` to configure classic Diffie-Hellman parameters
for DHE cipher suites. DockYarp has no equivalent. The item's own assessment already suspected this is
low-value on a modern stack (TLS 1.3 uses ECDHE, not classic DH) — this change confirms that with an actual
check of what Kestrel/.NET expose, rather than leaving the gap open indefinitely on an assumption.

## What Changes

- No behavior change. Confirms (via Microsoft documentation) that neither Kestrel nor the underlying TLS
  stack (SChannel on Windows, OpenSSL on Linux/macOS) expose an application-level knob for classic DH
  parameter *groups* (as opposed to cipher suite *selection*, which DockYarp already exposes via
  `TlsOptions.CipherSuites` on Linux/macOS) — DH group selection is either OS-policy-managed (Windows,
  via group policy/PowerShell TLS cmdlets, outside any .NET application's control) or not surfaced at all
  through `SslServerAuthenticationOptions`/`CipherSuitesPolicy`.
- Documents this explicitly in `docs/tls-acme.md` so the gap reads as an evaluated, closed non-goal rather
  than an open item — matching the item's own suggested outcome ("close as documented non-applicable").

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — no requirement-level behavior changes; see `skip_specs: true` in `.openspec.yaml`.)

## Impact

- `docs/tls-acme.md` only. No `src/` or test changes.
- Closes the backlog item and flips `openspec/backlog/parity.md`'s `DH params (DHPARAM_*, per-vhost)` row
  from ⛔ to ✅, with a note explaining the closure is by assessment (non-applicable on the modern stack
  DockYarp targets), not by building a matching feature — remove
  `openspec/backlog/items/add-dhparam-config.md` on archive per the standard lifecycle.
