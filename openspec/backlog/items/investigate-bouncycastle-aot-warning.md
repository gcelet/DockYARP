---
id: investigate-bouncycastle-aot-warning
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: low
status: backlog
nginx-proxy: n/a (internal finding — AOT/trim readiness, from investigate-certes-aot-alternative's own spike)
provenance: 2026-08-25 investigate-certes-aot-alternative's real -p:PublishAot=true spike — after removing
  Certes, this is the ONLY remaining warning (142 → 1)
---

## Why

After `investigate-certes-aot-alternative` removed Certes (142 → 1 total AOT/trim warnings), the single
remaining warning is:

```
Org.BouncyCastle.Utilities.Enums.GetEnumValues(Type): Using member 'System.Enum.GetValues(Type)' which has
'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.
```

— from `/_/crypto/src/util/Enums.cs(58)`, i.e. `Portable.BouncyCastle`'s own compiled source, not DockYarp's
code. The user intends to eventually enable Native AOT and wants the build as clean as possible before that
decision is made — this item tracks getting to a genuine **0-warning** baseline, not because AOT is being
adopted right now (`docs/aot-readiness.md` still frames that as a separate, open decision).

## nginx-proxy behavior

N/A — internal AOT/trim readiness, not a proxy-behavior parity gap.

## DockYarp today

`src/DockYarp.Tls/ClientCertificateValidator.cs` is the ONLY file using `Portable.BouncyCastle`
(`using Org.BouncyCastle.X509;`, one call site: `new X509CrlParser().ReadCrl(stream)` → `X509Crl`, enumerating
`X509CrlEntry` for CRL-based client-certificate revocation checking, from `add-mtls-optional-crl`).
`Directory.Packages.props` pins it at `1.9.0` specifically to avoid a `CS0433` conflict: the newer
`BouncyCastle.Cryptography` package's `Org.BouncyCastle.X509` types collide (identically named, same
namespace) with `Portable.BouncyCastle`'s own — so both can never coexist in the same project today.

**Already checked, don't re-derive:**
- `Portable.BouncyCastle` `1.9.0` is genuinely the latest published version (checked the real NuGet
  flatcontainer index) — no version bump exists that might have fixed this warning upstream.
- `BouncyCastle.Cryptography` (the actively maintained successor) is at `2.7.0` and still receiving regular
  releases — a real, live alternative exists, not a dead end like the original Certes situation.
- The BCL has no native CRL-parsing type (checked via `dotnet-inspect find "*Crl*" --platform` — zero
  results) — there is no way to drop BouncyCastle entirely for this use case.

## Proposed change (sketch)

**Most promising avenue**: migrate `ClientCertificateValidator.cs`'s one call site from `Portable.BouncyCastle`
to `BouncyCastle.Cryptography` and remove `Portable.BouncyCastle` entirely. With only one BouncyCastle package
in the graph, the `CS0433` conflict this pin was created to avoid no longer applies. Needs checking: does
`BouncyCastle.Cryptography`'s own `Org.BouncyCastle.X509.X509CrlParser`/`X509Crl`/`X509CrlEntry` API shape
match closely enough for a small, low-risk swap, and — the actual point of this item — does
`BouncyCastle.Cryptography`'s own `Enums.GetEnumValues` call site (if it has an equivalent) actually avoid the
`RequiresDynamicCodeAttribute` warning, or does the same warning simply move to the new package? Verify with a
real spike, not assumed.

**Fallback, only if the migration doesn't actually remove the warning**: a documented ILLink trim-warning
suppression (real mechanism, not a bare `#pragma`) with written justification — but only after confirming the
migration path genuinely doesn't help, per `AGENTS.md`'s "fix the root cause first" guardrail.

## Acceptance criteria (→ scenarios)

- **WHEN** a real `-p:PublishAot=true` spike is run after the change **THEN** the total warning count is 0.
- **WHEN** `ClientCertificateValidator.cs`'s CRL-based revocation check is exercised **THEN** behavior is
  unchanged (existing tests covering CRL revocation still pass, e2e mTLS-with-CRL scenarios still pass).

## Notes / risks / references

- Low priority: AOT itself is not adopted today: this is a "nice to have a clean baseline" item, not a
  blocker for anything currently shipping.
- Refs: `investigate-certes-aot-alternative`'s archived change (removed the other 141 warnings),
  `add-mtls-optional-crl`'s design.md (original rationale for pinning `Portable.BouncyCastle`),
  `docs/aot-readiness.md` (the warning-budget tracking doc to update once this lands).
