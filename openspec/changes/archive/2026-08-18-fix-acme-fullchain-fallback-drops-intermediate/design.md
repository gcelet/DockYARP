## Context

See `proposal.md` — Why / root cause (confirmed by decompiling `Certes.Pkcs.PfxBuilder` 3.0.4). Summary of the
relevant mechanics, since the fix depends on them precisely:

- `CertesAcmeClient.RequestCertificateAsync` generates the CSR itself and holds the exact `IKey privateKey`
  (hardcoded `KeyAlgorithm.ES256`) used for it — unlike the PEM-loading path, there is no ambiguity about which
  returned certificate the private key belongs to; it is always `CertificateChain.Certificate` (the leaf), by
  construction.
- `CertificateChain` exposes `.Certificate` (`IEncodable`, the leaf) and `.Issuers` (`IList<IEncodable>`,
  everything else the ACME server returned) directly — no path-building, no root requirement. `IEncodable`
  exposes `ToDer()`/`ToPem()`. `IKey` also implements `IEncodable` (so the private key is exportable as PEM too).
- The current bug is entirely inside `PfxBuilder.Build`'s `FullChain = true` branch (BouncyCastle
  `PkixCertPathBuilder`, requires a self-signed root among the returned certs) and its `FullChain = false`
  fallback (packages the leaf **only**, dropping `Issuers` even though they were available).

## Goals / Non-Goals

**Goals:**
- ACME-issued certificates preserve every certificate the ACME server returned (leaf + all issuers), regardless
  of whether a self-signed root is among them.
- No behavior change for a CA that does bundle its root (still preserved, still served).

**Non-Goals:**
- Fetching a missing root over the network (AIA-style) to complete a chain the ACME server didn't return one
  for — out of scope; DockYarp serves exactly what the CA gave it, same principle as the PEM-loading path.
- Changing anything about the PEM-loading (`PemCertificateLoader`) or serving (`SniTlsHandshakeCallback`) paths
  — both are already correct per `fix-tls-chain-not-sent-in-handshake`; this change only fixes chain
  *assembly* for the ACME path, upstream of them.

## Decisions

**Stop using `PfxBuilder`/`CertificateChain.ToPfx()` entirely; build the `LoadedCertificate` directly from
`CertificateChain.Certificate` + `CertificateChain.Issuers`.**

Rationale: `PfxBuilder.FullChain`'s PKIX path-building requires a property (a self-signed root present in the
ACME response) that is neither guaranteed by the ACME protocol nor actually needed — DockYarp does not need to
*validate* a path to a root, only to *serve* whatever certificates the CA returned. Alternatives considered:

- *Keep `PfxBuilder`, fix only the `FullChain = false` fallback to also bundle `Issuers`.* Rejected: still
  depends on the brittle `FullChain = true` attempt failing/succeeding in a CA-behavior-dependent way before
  reaching the fallback, and still routes through BouncyCastle's PKIX machinery for no benefit. Simpler and
  more robust to never attempt path-building at all.
- *Reuse `PemCertificateLoader`'s try-each-candidate key-matching logic.* Unnecessary here — the ACME path
  already knows deterministically which certificate the private key belongs to (`CertificateChain.Certificate`,
  the CSR's own subject), so there is no candidate to search for. Attach the key directly.

**Mechanics**: export `CertificateChain.Certificate` (leaf, DER) via `X509CertificateLoader.LoadCertificate`,
attach the private key directly (`privateKey.ToPem()` → `ECDsa.ImportFromPem` for ES256, matching the
hardcoded `KeyFactory.NewKey(KeyAlgorithm.ES256)` a few lines above in the same method — no RSA branch needed
since this codebase never requests an RSA ACME key), export each `Issuers[i]` (DER) the same way, assemble an
`X509Certificate2Collection { keyedLeaf, ...issuers }`, export as PKCS12, and load it back through the already-
existing `CertificateCollectionLoader.LoadKeyed` — the exact same helper `PemCertificateLoader` and
`FileCertificateStore` already use, so the resulting `LoadedCertificate` shape and round-trip behavior is
identical across all three loading paths.

**Verify (not assume) whether the e2e `step-ca` fixture's ACME response bundles its own root**, since that is
the leading hypothesis for why `fix-tls-chain-not-sent-in-handshake`'s own e2e coverage did not catch this bug.
If it does, adjust the fixture (or add a second one) so the regression test actually exercises the
no-root-in-response shape — otherwise the new unit test is the only thing standing between this bug and a
recurrence, and unit tests alone did not catch the *original* chain-dropping bug either (per this project's own
established lesson from `fix-tls-chain-not-sent-in-handshake`).

## Risks / Trade-offs

- [Risk] The e2e `step-ca` fixture might not be adjustable to omit its root without deeper changes to the
  `smallstep/step-ca` container config, which is out of this change's easy control. → Mitigation: if the
  fixture genuinely cannot be made to omit its root, the unit test (a `CertificateChain`-shaped fixture with
  issuers but no self-signed root, built directly against `CertesAcmeClient.BuildPfx`'s new logic) becomes the
  primary regression guard for this exact shape, and the e2e test still proves the already-covered
  root-bundled case did not regress.
- [Risk] `Issuers` ordering or content assumptions turn out to differ from what was empirically observed against
  the real private CA. → Mitigation: the new logic makes no ordering assumption — it treats `Issuers` as an
  unordered set added alongside the leaf, exactly like `PemCertificateLoader` already does for a PEM file's
  certificates regardless of order (see its "reversed order" test).
