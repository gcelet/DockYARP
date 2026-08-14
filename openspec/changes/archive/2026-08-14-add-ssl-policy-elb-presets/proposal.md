## Why
nginx-proxy's `SSL_POLICY` accepts, besides the Mozilla presets, the AWS ELB security-policy names (e.g.
`ELBSecurityPolicy-TLS13-1-2-2021-06`, `ELBSecurityPolicy-FS-1-2-Res-2020-10`). DockYarp recognizes only the three
Mozilla presets, so an ELB name is unrecognized and falls back to the global posture. Operators migrating from an AWS
ALB cannot reuse their policy name.

## What Changes
- Recognize the **classic AWS ELB (ALB) security-policy names** in `SslPolicyPresets`, both globally (`Tls:SslPolicy`)
  and per-host (`SSL_POLICY`). Each ELB name maps to:
  - a **TLS-version floor** — `Tls13` for the TLS-1.3-only policy, otherwise `Tls12` (DockYarp never enables TLS 1.0
    or 1.1, so every policy that permits an older minimum is clamped to DockYarp's 1.2 floor);
  - a **best-effort cipher set** reusing DockYarp's existing Mozilla suite arrays by tier (modern / intermediate /
    old) — DockYarp expresses ciphers as IANA suite names applied on Linux/macOS only, so the exact AWS OpenSSL cipher
    lists are approximated rather than reproduced verbatim.

## Honest scope (this is a thin, parity-tick change)
Because DockYarp floors at TLS 1.2 and does not model FIPS/PQ/RFC 9151 ciphers, the ~40 AWS policies **collapse** to
two effective TLS-version outcomes (`Tls12`/`Tls13`) plus best-effort ciphers. This change therefore:
- maps the **16 classic ALB policies** (the ones a typical operator names): `ELBSecurityPolicy-2016-08`, the
  `TLS-1-1/1-2(/Ext)` family, the `FS-*` family, and the `TLS13-1-0/1-1/1-2(/Res/Ext1/Ext2)/1-3-2021-06` family;
- **does not** map the specialized `-FIPS-*`, `-PQ-*`, and `-RFC9151-*` variants — their cipher requirements cannot be
  represented, so they keep the existing unrecognized-fallback behavior (global posture, one-time diagnostic).

## Capabilities
### Modified Capabilities
- `tls-acme`: the recognized `SSL_POLICY`/`Tls:SslPolicy` preset set includes the classic AWS ELB names.

## Impact
- **Code**: `DockYarp.Tls/SslPolicyPresets.cs` only — 16 dictionary entries reusing the existing `Tls13Suites`/
  `IntermediateSuites`/`OldSuites` arrays (no new cipher data). No behavior change for the Mozilla presets or the
  unrecognized-fallback path.
- **Tests**: `DockYarp.Tls.Tests/SslPolicyPresetsTests` — an ELB name resolves to the expected version floor + ciphers;
  the 1.3-only policy → `Tls13`; a specialized `-FIPS-*` name still falls back; the explicit-cipher override still wins.
- **Docs (user-facing — recognized config values)**: docs site `configuration.md` / `features.md` note that
  `SSL_POLICY` also accepts the classic AWS ELB policy names (clamped to the TLS 1.2 floor, best-effort ciphers).
- **Owning agent**: AG-AT. Sibling (done): `add-ssl-policy-presets`, `add-per-vhost-ssl-policy`.
