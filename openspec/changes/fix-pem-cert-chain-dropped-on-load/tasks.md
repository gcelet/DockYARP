## 1. Fix the loader (AG-AT)

- [x] 1.1 `src/DockYarp.Tls/PemCertificateLoader.cs`: replaced the single-cert `X509Certificate2.CreateFromPem`
      call — parses all certificates via `X509Certificate2Collection.ImportFromPem`, tries RSA then EC via
      `CopyWithPrivateKey` against each candidate (not assuming the first is the leaf; `ArgumentException` on
      mismatch, confirmed via Microsoft's own docs before relying on it), keeps the first one that matches.
- [x] 1.2 Exports the resulting collection as PKCS12 and reloads via `X509CertificateLoader.LoadPkcs12` —
      mirrors `FileCertificateStore`'s existing `.pfx` branch and `CertesAcmeClient.BuildPfx`'s chain-inclusive
      shape.
- [x] 1.3 Single-certificate case: unchanged in effect — the loop finds the one candidate on its first (only)
      iteration, same round-trip-through-PKCS12 behavior as before. Verified by the existing
      `PemPairIsLoaded`/`UnpairedCertificateIsSkipped` tests still passing (see section 2.5).
- [x] 1.4 Key-matches-nothing: `TryBuildKeyedChain` returns `false` when no candidate matches, `TryLoad`
      surfaces that as its existing `false`-return contract — no new exception type introduced.

## 2. Test fixture: a real leaf + intermediate chain (AG-AT)

- [x] 2.1 New `tests/DockYarp.Tls.Tests/TestChainFactory.cs`: builds a real 2-certificate chain (self-signed
      intermediate CA via `CertificateRequest` + a leaf signed by it), returning independent, caller-owned
      `X509Certificate2` instances.
- [x] 2.2 `CertificateStoreTests.FullChainPemPreservesIntermediate`: leaf+intermediate PEM (leaf first) loads,
      `HasPrivateKey` is true, and a `CustomRootTrust` `X509Chain.Build` against *only* the intermediate (empty
      `ExtraStore`) succeeds — this can only succeed if the loaded certificate itself carries the intermediate,
      so it proves preservation, not just that loading didn't throw.
- [x] 2.3 `FullChainPemPreservesIntermediate_ReversedOrder`: same fixture, intermediate first in the file —
      confirms the "try each candidate" key-matching approach, not an assumption about file order.
- [x] 2.4 `KeyMatchingNoCertificateIsSkipped`: a `.crt` (leaf+intermediate) paired with an unrelated key — the
      documented skip/`null` behavior, not a crash.
- [x] 2.5 Full non-E2E suite re-run: 396/396 green (up from 393 — the 3 new tests), including the pre-existing
      `PemPairIsLoaded`/`UnpairedCertificateIsSkipped` — no regression.

## 3. Spec sync prep (AG-AT)

- [x] 3.1 Verified the delta spec's MODIFIED "Provided certificate loading" requirement (the new "full-chain
      PEM file preserves its intermediate" scenario) matches what actually shipped in sections 1-2.
