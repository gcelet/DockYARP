# Design — add-ssl-policy-elb-presets

## The collapse (why this is thin)
DockYarp's `TlsVersion` enum is `{ Tls12, Tls13 }` — it floors at TLS 1.2 and never enables 1.0/1.1. AWS publishes ~40
ELB security policies differing by protocol floor (1.0–1.3) and cipher list. From DockYarp's perspective every policy
reduces to:
- **`Tls13`** — only the TLS-1.3-only policy (`ELBSecurityPolicy-TLS13-1-3-2021-06`);
- **`Tls12`** — every other policy (its ≥1.2 or clamped-to-1.2 floor).

Ciphers are best-effort: DockYarp expresses ciphers as IANA suite names and applies them on Linux/macOS only
(`TlsHardening.ParseCipherSuites` drops unknown names; SChannel picks its own on Windows). AWS lists OpenSSL names
(`ECDHE-RSA-AES128-GCM-SHA256`), which would all be dropped — so reproducing them verbatim is pointless. Instead each
ELB policy reuses the nearest existing Mozilla suite array by tier.

## Mapping (16 classic ALB policies → existing arrays)
Add these to `SslPolicyPresets.Presets` (case-insensitive), reusing `Tls13Suites` / `IntermediateSuites` / `OldSuites`:

| ELB policy | Version | Ciphers (reused array) |
|---|---|---|
| `ELBSecurityPolicy-TLS13-1-3-2021-06` | `Tls13` | `Tls13Suites` |
| `ELBSecurityPolicy-TLS-1-2-2017-01` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-FS-1-2-2019-08` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-FS-1-2-Res-2019-08` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-FS-1-2-Res-2020-10` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-TLS13-1-2-2021-06` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-TLS13-1-2-Res-2021-06` | `Tls12` | `IntermediateSuites` |
| `ELBSecurityPolicy-2016-08` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS-1-1-2017-01` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS-1-2-Ext-2018-06` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-FS-2018-06` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-FS-1-1-2019-08` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS13-1-0-2021-06` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS13-1-1-2021-06` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS13-1-2-Ext1-2021-06` | `Tls12` | `OldSuites` |
| `ELBSecurityPolicy-TLS13-1-2-Ext2-2021-06` | `Tls12` | `OldSuites` |

Rationale for the cipher tier: **Intermediate** for the GCM/FS-only "restricted/modern-1.2" policies; **Old** for the
broader policies that include CBC-SHA suites (`2016-08`, `TLS-1-1`, `-Ext*`, `FS-2018-06`, `FS-1-1`, `TLS13-1-0/1-1`).
The exact suite set is not load-bearing (best-effort, dropped on Windows); the **version floor** is the reliable part.

## Not mapped (kept as unrecognized → global fallback)
The `-FIPS-*`, `-PQ-*` (post-quantum), and `-RFC9151-*` variants require ciphers DockYarp does not model. They stay
unrecognized: the existing behavior applies (global posture + one-time diagnostic). Documented, not silently wrong.

## No mechanism change
`SslPolicyPresets.Resolve` already looks up `Presets` case-insensitively and lets an explicit cipher list override the
preset; adding entries is purely additive. Both `Tls:SslPolicy` (global) and per-host `SSL_POLICY` resolve through the
same table, so both gain the ELB names at once. `KnownPresetNames` (used by the unrecognized-policy diagnostic) grows
accordingly.

## Tests (`DockYarp.Tls.Tests/SslPolicyPresetsTests`)
- `ELBSecurityPolicy-TLS13-1-3-2021-06` → `Tls13` + `Tls13Suites`;
- `ELBSecurityPolicy-FS-1-2-Res-2020-10` → `Tls12` + `IntermediateSuites`;
- `ELBSecurityPolicy-2016-08` → `Tls12` + `OldSuites`;
- resolution is case-insensitive;
- a specialized `ELBSecurityPolicy-TLS13-1-2-FIPS-2023-04` is **not** recognized → falls back (configured values unchanged);
- an explicit `configuredCiphers` still overrides an ELB preset's ciphers.
