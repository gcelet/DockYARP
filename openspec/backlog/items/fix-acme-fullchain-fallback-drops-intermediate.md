---
id: fix-acme-fullchain-fallback-drops-intermediate
capability: tls-acme
agent: AG-AT
tier: A-unit
priority: high
status: backlog
provenance: real-world migration test against a genuinely private CA, found immediately after fix-tls-chain-not-sent-in-handshake shipped
---

## Why

`fix-tls-chain-not-sent-in-handshake` fixed the TLS-serving side of chain preservation (`SslStreamCertificateContext`) and the PEM-loading side (`PemCertificateLoader`), and specifically claimed to also fix `CertesAcmeClient`'s
identical single-cert-loading bug. It did — but a separate, deeper bug in the *chain-building* step upstream of
that fix silently defeats it for a real, common class of private CAs: the resulting certificate is served
leaf-only again, exactly the symptom the whole effort was meant to eliminate.

Confirmed live: an ACME-issued certificate from a private CA was served with only its leaf certificate in the
handshake (`openssl s_client -showcerts` showed a single certificate), even though the same CA's certificates
issued to other, non-DockYarp-fronted hosts correctly carry their intermediate. `Verification error: unable to
verify the first certificate` in the openssl output.

## Root cause (confirmed by decompiling Certes 3.0.4, not assumed)

`CertesAcmeClient.BuildPfx` (`src/DockYarp.Tls/CertesAcmeClient.cs`) calls `chain.ToPfx(privateKey).Build(...)`
with `PfxBuilder.FullChain = true` first, falling back to `FullChain = false` on `AcmeException`.

Decompiled `PfxBuilder.Build`/`FindIssuers`:
- `FullChain = true` invokes BouncyCastle's `PkixCertPathBuilder` to build a certification **path that must
  terminate at a self-signed certificate found among the issuers the ACME server returned**.
- A CA that follows normal ACME/PKI convention — the root is trusted out of band, never distributed via the
  protocol — returns leaf + intermediate(s) only, **no self-signed root**. Path-building then has no trust
  anchor to terminate at and fails.
- The `FullChain = false` fallback packages **leaf only** — it does not fall back to "leaf + whatever
  intermediates were returned," it discards the intermediates entirely, even though
  `CertificateChain.Issuers` (the raw list Certes received from the ACME server) still has them.

This means: **any private CA that does not bundle its own root into the ACME response — which is the norm,
not the exception — causes DockYarp to silently serve a leaf-only certificate**, regardless of the
`fix-tls-chain-not-sent-in-handshake`/`LoadedCertificate` machinery downstream, because the intermediate never
survives this earlier step to reach it.

**Why this was not caught by `fix-tls-chain-not-sent-in-handshake`'s own e2e coverage**: not yet confirmed
empirically, but the leading hypothesis is that the e2e suite's `step-ca` container (default
`smallstep/step-ca` config) *does* bundle its self-signed root into the ACME response, letting the PKIX path
succeed in that specific case and masking the bug — while the CA that surfaced this in the real-world test does
not. This must be verified (not assumed) as part of this fix's design, and the e2e fixture adjusted to actually
exercise the no-root-in-response shape if that's confirmed.

## Proposed change (sketch)

Stop depending on Certes' `PfxBuilder.FullChain`/PKIX path-building for chain assembly entirely — it requires
a property (a self-signed root in the response) that is not guaranteed and is not needed for what DockYarp
actually wants (serve everything the CA gave us, no path validation). Instead, build the chain-inclusive
certificate directly from `CertificateChain.Certificate` (leaf) + `CertificateChain.Issuers` (unconditionally,
whatever the ACME server returned), reusing the same robust pattern `PemCertificateLoader`/
`CertificateCollectionLoader` already use for the provided-certificate path — parse everything, key-match the
leaf, keep the rest as `Additional`, no root/path requirement anywhere.

## Acceptance criteria (→ scenarios)

- **WHEN** an ACME provider issues a certificate whose chain includes an intermediate but does **not** include
  a self-signed root in the response
- **THEN** the certificate served for that host still includes the intermediate (verified at the wire level,
  not just by inspecting the loaded object)
- **WHEN** an ACME provider issues a certificate whose chain (root included) already lets the current fallback
  succeed
- **THEN** behavior is unchanged (no regression for CAs that do bundle their root)

## Notes / risks / references

- `src/DockYarp.Tls/CertesAcmeClient.cs` (`BuildPfx`, `RequestCertificateAsync`).
- Decompiled via `dnx dotnet-inspect` against the exact `Certes 3.0.4` package version this project references
  — re-verify against whatever version is actually pinned in `Directory.Packages.props` at implementation time.
- Real wire-level e2e coverage is required here, same bar as `fix-tls-chain-not-sent-in-handshake` — a unit
  test alone would not have caught the original bug either. If the `step-ca` e2e fixture turns out to bundle
  its root (masking this exact bug), the fixture itself needs adjusting so the regression test actually
  exercises the no-root-in-response shape, not just re-running the existing (masked) scenario.
- No parity.md row expected — internal correctness bug, not a parity-matrix feature.
