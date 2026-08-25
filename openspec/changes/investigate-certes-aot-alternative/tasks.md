## 1. ACME JSON models and JWS/JWK helpers (AG-AT)

- [x] 1.1 Create `src/DockYarp.Tls/Acme/Acme{Directory,NewAccountRequest,Identifier,NewOrderRequest,Order,
      FinalizeRequest,Authorization,Challenge,ProblemDetails,JsonContext}.cs` — one file per type (MA0048),
      field shapes per RFC 8555 §7.1.1-7.1.6/§7.4/§8.3, and `AcmeJsonContext : JsonSerializerContext` with
      `[JsonSerializable]` for every type. Verified `dotnet build src/DockYarp.Tls/DockYarp.Tls.csproj`
      succeeds, 0 warnings.
- [x] 1.2 Create `src/DockYarp.Tls/Acme/AcmeJws.cs`: JWK construction from an `ECDsa` public key (RFC 7518
      §6.2.1 field order), RFC 7638 JWK thumbprint, and JWS assembly/signing via
      `ECDsa.SignData(..., DSASignatureFormat.IeeeP1363FixedFieldConcatenation)`. `Sign`'s parameter count
      was bundled into a `JwsRequestContext` record struct to satisfy AV1561 (max 5 params). Verified build,
      0 warnings.
- [x] 1.3 Added `tests/DockYarp.Tls.Tests/AcmeJwsTests.cs`: JWK field/coordinate match, thumbprint
      determinism/uniqueness, DNS-01 TXT self-consistency (independently recomputed SHA-256), JWS structure
      (jwk vs kid header, empty payload for POST-as-GET), and a real signature-verification self-consistency
      test (`ECDsa.VerifyData` against the produced signature) — mirrors `DnsUpdateMessageTests`'s own
      self-consistency approach rather than an unconfirmed RFC vector. 7/7 tests pass.

## 2. ACME HTTP client (AG-AT)

- [x] 2.1 Created `src/DockYarp.Tls/Acme/AcmeHttpClient.cs`: directory discovery, `Replay-Nonce` tracking,
      JWS-signed POST + POST-as-GET helpers (using `AcmeJsonContext` explicitly at every call site), one
      bounded retry on `badNonce`, `problem+json` error surfacing. `CreateAccountAsync` takes the already-
      built `AcmeNewAccountRequest` rather than a raw `bool` param (AV1564 — self-documenting at the call
      site, not a suppression). Verified build, 0 warnings.
- [x] 2.2 Added `tests/DockYarp.Tls.Tests/AcmeHttpClientTests.cs` with a queued fake `HttpMessageHandler`:
      nonce fetched once then reused/refreshed from each response, one bounded `badNonce` retry that
      succeeds, immediate throw on a non-`badNonce` error, and throw after exhausting the single retry.
      4/4 tests pass.

## 3. Replace CertesAcmeClient (AG-AT)

- [x] 3.1 Renamed `src/DockYarp.Tls/CertesAcmeClient.cs` to `src/DockYarp.Tls/AcmeClient.cs` (`git mv`),
      reimplemented `RequestCertificateAsync`/`CompleteHttpChallengeAsync`/`CompleteDnsChallengeAsync`/
      `WaitForValidationAsync` against `AcmeHttpClient`, added `WaitForCertificateAsync` (polls the order via
      the new `GetOrderAsync` until `certificate` is set, mirroring the authorization-status poll). Bundled
      the challenge-completion parameters into a nested `ChallengeContext` record struct (AV1561, same
      pattern as `AcmeJws.JwsRequestContext`). Verified build, 0 errors.
- [x] 3.2 Reimplemented `BuildLoadedCertificate` to take the concatenated PEM chain string + `ECDsa` leaf key
      directly, using `X509Certificate2Collection.ImportFromPem` instead of Certes' `CertificateChain`,
      preserving the "no self-signed-root PKIX path required" behavior (leaf keyed via `CopyWithPrivateKey`,
      every other imported cert added as-is). Found and adapted an EXISTING test file
      (`CertesAcmeClientTests.cs` → `AcmeClientTests.cs`, `git mv`) covering exactly this behavior (chain
      with/without a returned root) — rebuilt its fixture on plain `ECDsa`/PEM strings instead of Certes
      types. 2/2 tests pass, including the real regression case (no root in the response).
- [x] 3.3 Implemented CSR generation via `CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256)
      .CreateSigningRequest()` — verified as part of 3.1/3.2's build.
- [x] 3.4 Updated `TlsServiceCollectionExtensions.cs`'s DI registration to `AcmeClient`. Verified
      `dotnet build DockYarp.slnx` succeeds with 0 errors (only the pre-existing, unrelated ASPIRE010
      warning on the E2E AppHost project).

## 4. Remove the Certes dependency (AG-AT)

- [x] 4.1 Removed the `Certes` `PackageVersion`/`PackageReference` from `Directory.Packages.props` and
      `DockYarp.Tls.csproj`. Also updated `Portable.BouncyCastle`'s own comment (previously justified by
      "Certes pulls this in transitively") to reflect it's now DockYarp.Tls's own direct, standalone CRL-
      parsing dependency. Verified `dotnet restore DockYarp.slnx` succeeds, 0 errors.
- [x] 4.2 Grepped the whole solution for `Certes`/`Newtonsoft` — zero package/using references remain.
      3 stale comments referencing the old `CertesAcmeClient` name or "Certes uses the default HttpClient"
      found and fixed (`PemCertificateLoader.cs`, `tests/DockYarp.E2E.AppHost/Program.cs`,
      `tests/DockYarp.E2E.Tests/TlsTests.cs`) — the remaining hits are intentional historical-context comments
      in the new `Acme/` files explaining behavior parity with the old client. Full solution build: 0 errors.

## 5. Verify (AG-AT)

- [x] 5.1 Ran `dotnet build DockYarp.slnx` — 0 errors (1 pre-existing, unrelated ASPIRE010 warning).
- [x] 5.2 Ran `dotnet test DockYarp.slnx` (excluding E2E) — 484/484 unit/integration tests pass.
- [x] 5.3 Ran the full E2E suite (`./build.ps1 E2E`) twice. **First real run against step-ca found a genuine
      protocol bug, not caught by unit tests**: step-ca rejected every `new-account` request with
      `"The request message was malformed"` — root-caused via step-ca's own server log:
      `expected content-type to be in [application/jose+json], but got application/jose+json; charset=utf-8`
      (`StringContent`'s 3-arg constructor appends a charset RFC 8555 §6.2 doesn't allow). Fixed by clearing
      `content.Headers.ContentType.CharSet` after construction. **Re-ran twice more after the fix**: once via
      a direct `dotnet test` (reused the already-rebuilt image, 42/42 in 14s — inconclusive alone per
      [[e2e-fast-iteration-stale-image]]), then via the full `./build.ps1 E2E` pipeline with a genuinely fresh
      image rebuild — **42/42 passing, 1m28s, matching the established healthy baseline** — the authoritative
      confirmation. (A first `./build.ps1 E2E` attempt right after the fix failed at 1m36s; re-root-caused as
      stale leftover container/volume state from the earlier crash-looping pre-fix containers, not a
      persisting bug — resolved by force-removing the orphaned containers before the clean rerun.)
- [x] 5.4 Ran the real `-p:PublishAot=true` spike per `docs/aot-readiness.md`'s documented command, `obj`/`bin`
      cleared first for every touched/dependent project as the doc requires. **Result: 142 → 1 total AOT/trim
      warning.** The single remaining warning is the already-known, unrelated `Org.BouncyCastle.Utilities`
      one (`Portable.BouncyCastle`, CRL parsing) — already documented in the baseline table, untouched by
      this change. The entire ~141-warning Newtonsoft.Json/Certes bucket, including its downstream
      `System.Linq.Expressions`/`Microsoft.CSharp.RuntimeBinder` consequences, is gone.

## 6. Documentation (AG-AT)

- [x] 6.1 Updated `docs/aot-readiness.md`'s baseline (142→1), removed the now-resolved Certes/Newtonsoft
      table rows, and added 2 "Lessons that will bite again" entries (the verify-before-concluding-blocked
      precedent, and the real-server-vs-unit-test bug-surfacing lesson from the charset bug).
- [x] 6.2 Grepped `docs-site/`, `docs/labels-reference.md`, `README.md` for "Certes" — zero hits, confirming
      it was never a user-facing implementation detail. No update needed.
