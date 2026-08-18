## Why

`fix-tls-chain-not-sent-in-handshake` fixed chain preservation for ACME-issued certificates at the loading
and serving layers, but a deeper bug in the chain-*assembly* step upstream of both silently defeats it for any
private CA that follows normal ACME convention (no self-signed root in the ACME response — the client trusts
the root out of band). Confirmed live against a real private CA: the served certificate was leaf-only again,
`openssl s_client` reporting `unable to verify the first certificate`, exactly the symptom the prior fix was
meant to eliminate.

## What Changes

- `CertesAcmeClient.BuildPfx` stops relying on Certes' `PfxBuilder.FullChain`/PKIX path-building (which
  requires a self-signed root among the certificates the ACME server returned — not guaranteed, and not
  actually needed). It builds the chain-inclusive certificate directly from `CertificateChain.Certificate`
  (leaf) + `CertificateChain.Issuers` (unconditionally, whatever the CA returned), reusing the same
  `CertificateCollectionLoader`/PKCS12 pattern already used for the provided-certificate (PEM/PFX) path — no
  root/path requirement anywhere.
- The `fix-pem-cert-chain-dropped-on-load`/`fix-tls-chain-not-sent-in-handshake` e2e `step-ca` fixture is
  checked for whether it already bundles its own root in the ACME response (the leading hypothesis for why the
  prior e2e coverage did not catch this). If it does, the fixture is adjusted so the regression test actually
  exercises the no-root-in-response shape — the exact shape that broke in the real world.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `tls-acme`: "ACME certificate acquisition" — tightens the existing chain-preservation guarantee to hold
  regardless of whether the ACME provider's response includes its own root certificate, closing the gap the
  prior change's wording left open.

## Impact

- `src/DockYarp.Tls/CertesAcmeClient.cs` (`BuildPfx`, `RequestCertificateAsync`).
- `tests/DockYarp.Tls.Tests/` — unit coverage for the new chain-assembly path (a fixture with issuers but no
  self-signed root among them, alongside the existing bundled-root case).
- `tests/DockYarp.E2E.Tests/` + `tests/DockYarp.E2E.AppHost/` — real wire-level e2e coverage proving the
  no-root-in-response shape specifically, not just re-running the existing (possibly root-bundling) `step-ca`
  scenario.
- No parity.md row expected — internal correctness bug, not a parity-matrix feature.
