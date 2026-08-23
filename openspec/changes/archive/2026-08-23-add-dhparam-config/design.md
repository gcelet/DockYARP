## Context

See `proposal.md` for the motivation. `TlsOptions.CipherSuites` already exists (Linux/macOS only) for
restricting *which* cipher suites are negotiable — a different mechanism from nginx's `DHPARAM_*`, which
configures the *DH group/parameters* used by DHE key exchange specifically.

## Goals / Non-Goals

**Goals:**
- Determine, from Microsoft's own documentation, whether Kestrel/.NET expose any application-level control
  over classic DH parameters (group size, custom `dhparam.pem`-equivalent) for TLS 1.2 DHE cipher suites.
- Record the answer in `docs/tls-acme.md` so this reads as an evaluated decision, not an unexamined gap.

**Non-Goals:**
- Adding any new configuration surface — this change is documentation-only, contingent on the assessment
  confirming no meaningful knob exists (expected outcome per the backlog item's own note).

## Decisions

- **Assessment (2026-08-23, via Microsoft Learn documentation)**: DH parameter *group* selection is not
  exposed through `SslServerAuthenticationOptions`/`SslStream`/`CipherSuitesPolicy` on any platform. On
  Windows, TLS cipher suite and DH-group policy is entirely OS-managed (SChannel), configurable only via
  Group Policy or the `TLS` PowerShell module — outside any .NET application's reach, consistent with
  `docs/tls-acme.md`'s existing note that `CipherSuites` "is ignored where the platform manages ciphers,
  e.g. Windows." On Linux/macOS, .NET's `CipherSuitesPolicy` (already exposed via `TlsOptions.CipherSuites`)
  restricts which negotiated cipher suites are allowed but does not expose the underlying OpenSSL DH-group
  parameters themselves. TLS 1.3 — DockYarp's default (`TlsOptions.MinimumTlsVersion`) — does not use
  classic DH parameters at all (it uses a fixed, negotiated set of named groups, not custom DH params).
- **Decision**: close as documented non-applicable, exactly as the backlog item itself anticipated. Add a
  short note to `docs/tls-acme.md`'s `Configuration (TlsOptions)` section, next to the existing
  `CipherSuites` bullet, so the two related TLS-tuning knobs are documented together.

## Risks / Trade-offs

- [Risk] A future .NET release could add DH-group control that doesn't exist today. → Low risk given TLS
  1.3's design trend away from classic DH; if it matters later, the parity.md note points back to this
  change's reasoning to revisit cheaply.
