## 1. CRL revocation (AG-AT)

- [x] 1.1 `Directory.Packages.props`: add a BouncyCastle `PackageVersion`. **Correction found live**: the
      originally-planned `BouncyCastle.Cryptography` conflicts (`CS0433`) with `Portable.BouncyCastle`, which
      `Certes` already pulls in transitively — same `Org.BouncyCastle.X509` namespace, same type names, two
      different assemblies. Used `Portable.BouncyCastle` (pinned to Certes's own version, `1.9.0`) instead — see
      design.md's Decisions.
- [x] 1.2 `src/DockYarp.Tls/TlsOptions.cs`: add `ClientCrlPath` (`string?`, default `null`), mirroring
      `ClientCaCertificatePath`'s doc-comment style.
- [x] 1.3 `src/DockYarp.Tls/ClientCertificateValidator.cs`: load the CRL (when `ClientCrlPath` is set and the
      file exists) via BouncyCastle's `X509CrlParser`, extracting revoked serial numbers into a
      `FrozenSet<System.Numerics.BigInteger>` once at construction (mirrors the existing once-at-startup CA
      load; comparison done via a hex-string round-trip to avoid endian pitfalls, not raw byte arrays — see
      `ParseSerialHex`). `Validate` now also rejects a certificate whose serial is in that set. No BouncyCastle
      type in the public surface — signature stays `Validate(X509Certificate2) : bool`.
      `dotnet build DockYarp.slnx` — 0 warnings/errors.
- [x] 1.4 Unit tests (`DockYarp.Tls.Tests`): a certificate chaining to the CA but listed in a test CRL fixture
      (generate the fixture via BouncyCastle in the test itself, not an opaque pre-baked file — see design.md's
      Decisions) is rejected; a certificate not in the CRL is accepted; no CRL configured behaves exactly as
      before (CA-chain check only, covered by the two pre-existing tests, unmodified). 3/3 green
      (`RevokedCertificateFailsValidation` new, `ValidatesAgainstConfiguredCa`/`NoCaMeansNoClientAuth` unchanged).

## 2. Per-host handshake awareness (AG-AT)

- [x] 2.1 New `src/DockYarp.Tls/HostClientCertificateResolver.cs`: `Resolve(RouteConfigSnapshot, string host) :
      ClientCertificateRequirement`, mirroring `HostSslPolicyResolver`/`HostHttp2Resolver` exactly (iterate
      `snapshot.Routes`, `HostPattern.Parse(route.HostPattern).Matches(host)`, return the first match's
      `ClientCertificate`; `None` when no route matches). Written, builds clean.
- [x] 2.2 `src/DockYarp.Tls/SniTlsHandshakeCallback.cs`: replaced the single shared `validateClientCertificate`
      with two delegates — `strictValidateClientCertificate` (today's logic: `null` accepted, presented cert
      must pass `Validate()`) for `Required` hosts, and `permissiveValidateClientCertificate`
      (`(_, _, _, _) => true`, never fails the handshake) for `Optional` hosts. `BuildOptions(host)` resolves
      the host's requirement via task 2.1's resolver; sets `ClientCertificateRequired`/the callback only when
      the requirement is `Required` or `Optional` — a `None` host gets neither (no client-cert prompt at all).
      **No-SNI edge case handled**: when `host` is null/empty, falls back to `Required` (preserves the
      pre-change unconditional-strict behavior, since which host's policy would apply is unknowable — mirrors
      `ResolvePolicy`'s own no-SNI→global-posture precedent). Builds clean (`dotnet build DockYarp.slnx`: 0
      warnings/errors).
- [x] 2.3 Unit tests (`DockYarp.Tls.Tests`): `BuildOptions` for a `Required` host sets the strict callback and
      `ClientCertificateRequired = true`; for an `Optional` host sets the permissive callback and
      `ClientCertificateRequired = true`; for a `None` host (or when no CA is configured) sets neither. Fixed
      the pre-existing `MutualTlsWiredWhenClientCaConfigured` (now passes a `Required` route fixture via the new
      `RoutesWithClientCertificate` helper, mirroring `RoutesWithPolicy`/`RoutesWithHttp2`); added
      `OptionalHostNeverFailsHandshake` (permissive callback accepts both no-cert and an untrusted cert) and
      `NoneHostRequestsNoClientCertificate` (a host with no matching requirement gets neither
      `ClientCertificateRequired` nor a callback, even though a CA is configured for a different host). 10/10
      green.

## 3. Verification-status threading (AG-SEC)

- [x] 3.1 New `src/DockYarp.Core/Models/ClientCertificateVerificationStatus.cs`: enum `NotPresented` / `Verified`
      / `Failed`.
- [x] 3.2 `src/DockYarp.Security/ClientCertificateMiddleware.cs`: for a route whose `ClientCertificate` is
      `Required` or `Optional`, compute the status (`NotPresented` when `Connection.ClientCertificate` is
      `null`; else `Verified`/`Failed` via `ClientCertificateValidator.Validate` — now including CRL, task 1.3)
      and store it on `HttpContext.Items` under `ClientCertificateMiddleware.VerificationStatusKey` (a
      `public static readonly object` sentinel — public, not just private, so `ForwardedHeadersTransform` in
      `DockYarp.App` can read the same key; avoids a plain-string key collision — see design.md's Risks). For
      `None` routes, the item is not set (unchanged: no header later). The 403 check is now
      `Required && status != Verified` (functionally unchanged for `Required`: an invalid/revoked cert can never
      reach this code there, since task 2's strict callback already rejected it at the handshake).
      **Real architectural decision found live**: `ClientCertificateMiddleware` needs `ClientCertificateValidator`
      (CA-chain + CRL check) to compute the status for `Optional` routes, but `DockYarp.Security` only referenced
      `DockYarp.Core` — added `DockYarp.Security` → `DockYarp.Tls` `ProjectReference` (confirmed no cycle: `Tls`
      only references `Core`, doesn't reference `Security`). Reuses the existing validator rather than
      re-implementing chain/CRL logic in `Security` or engineering a connection-feature-passing mechanism from
      the handshake layer (the `RemoteCertificateValidationCallback` signature has no connection-context access
      to stash a precomputed result on, so re-validating once per request in the middleware — not per proxied
      request in the hot-path transform, see design.md's Decisions — was the simpler, still-correct choice).
- [x] 3.3 Unit tests (`DockYarp.Security.Tests`): `Required` + no cert → 403, status `NotPresented` (existing
      coverage rewritten — the old test's bare self-signed cert with no CA configured no longer represents a
      real scenario now that the middleware actually validates, so `Middleware(...)`'s helper now wires a real
      `ClientCertificateValidator` with a CA, mirroring `ClientCertificateValidatorTests`); `Required` + a
      CA-chained cert → continues, status `Verified`; `Optional` + `Verified`/`Failed`/`NotPresented` → never
      rejected (200), status recorded correctly for each; `None` → continues, item not set. Needed
      `System.IO.Abstractions.TestingHelpers` added to `DockYarp.Security.Tests.csproj` (version already
      centralized). 4/4 green.

## 4. Header contract (AG-RP)

- [x] 4.1 `src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs`: reads the `HttpContext.Items` entry from
      task 3.2 instead of checking `Connection.ClientCertificate` directly. `Verified` →
      `X-SSL-Client-Verify: SUCCESS` + `X-SSL-Client-S-DN`/`X-SSL-Client-I-DN`; `Failed` →
      `X-SSL-Client-Verify: FAILED` (no DN headers); `NotPresented` → `X-SSL-Client-Verify: NONE` (no DN
      headers). When the item is absent (route has no client-certificate requirement): no `X-SSL-Client-*`
      header, unchanged from today. Builds clean.
- [x] 4.2 Integration tests (`DockYarp.IntegrationTests`): **scope corrected from the original plan** — `SUCCESS`
      and `FAILED` genuinely need a real client certificate over a real TLS handshake, which
      `WebApplicationFactory`'s in-memory `TestServer` cannot simulate (no TLS layer at all in-process); those
      two cases are covered end-to-end instead (task 5.2), not here. Added at the integration level, where they
      ARE achievable without TLS: `OptionalRouteWithNoCertificateReportsNone` (an `Optional` route with no
      presented cert → `X-SSL-Client-Verify: NONE`, no DN headers) and
      `RouteWithoutRequirementGetsNoSslClientHeader` (a route with no requirement → no header at all, distinct
      from `NONE`). The pre-existing `SpoofedClientCertHeadersAreStripped` (stripping is unconditional, per the
      spec delta's corrected scenario wording) still passes unmodified. 6/6 green.

## 5. End-to-end validation (AG-AT)

- [x] 5.1 Read `tests/DockYarp.E2E.AppHost/BackendCatalog.cs`'s existing `echo-mtls` backend
      (`DOCKYARP_CLIENT_CERT=required`) and its e2e test (`MutualTls_RejectsWithoutClientCertificate` /
      `MutualTls_AcceptsValidClientCertificate`, per `docs/testing.md`'s coverage map) before adding new
      coverage — confirmed the exact TLS client-configuration pattern already used (`CertificateChainPolicy`
      with `CustomRootTrust`) and mirrored it throughout.
- [x] 5.2 New e2e backend `echo-mtls-optional` (`mtls-optional.local`, `DOCKYARP_CLIENT_CERT=optional`) +
      3 scenarios: `MutualTlsOptional_NoCertificateSucceedsAsNone`,
      `MutualTlsOptional_UntrustedCertificateSucceedsAsFailed` (a real TLS handshake genuinely NOT dropping an
      untrusted client cert — the one behavior that cannot be proven below e2e, the real proof this item exists
      for), `MutualTlsOptional_ValidCertificateSucceedsAsSuccess`. `TlsHarness` gained
      `CreateClientPresentingCertificate` (a generalized `CreateMutualTlsClient`) and a local
      `CreateUntrustedClientCertificate` helper in `TlsTests.cs`.
- [x] 5.3 New e2e scenario `MutualTlsRequired_RevokedCertificateIsRejected`: a CRL-revoked client certificate
      presented to `mtls.local` (required) fails the TLS handshake itself (asserted via `ThrowAsync
      <HttpRequestException>`, not a 403 — see the test's own remarks on why). `TlsHarness.PrepareClientCa()`
      now also issues a second leaf (`RevokedClientCertificate`) and writes a real BouncyCastle-signed CRL
      revoking its serial to the new `E2EPaths.ClientCrlFile` (same directory as the CA, no new bind mount);
      the AppHost's `dockyarp` resource gained `Tls__ClientCrlPath=/clientca/client-ca.crl`.
      **Three real bugs found and fixed during validation, not assumed correct from the first attempt**:
      (1) `CreateUntrustedClientCertificate` called `CopyWithPrivateKey` on a `CreateSelfSigned()` result, which
      already carries its private key — threw `InvalidOperationException`; fixed by exporting `leaf` directly.
      (2) The CRL was first written PEM-wrapped (hand-rolled `-----BEGIN X509 CRL-----` markers), assuming
      `X509CrlParser.ReadCrl(Stream)` auto-detects PEM — unverified and likely wrong; fixed to write raw DER
      bytes via `crl.GetEncoded()`, matching `ClientCertificateValidatorTests`' own already-proven-working
      fixture approach exactly, removing the assumption entirely. (3) **The real root cause of all three
      e2e failures on the first attempt**: the running `dockyarp:local` Docker image was stale — built
      2026-08-22T14:34Z, *before* this session's mTLS/CRL source changes existed, because `dotnet test` on the
      E2E test project directly (used for fast iteration all session) never triggers the image rebuild step
      that only the full `./build.ps1 E2E`/Fallout pipeline runs. All three failures' symptoms were consistent
      with old pre-this-change behavior once traced through the dockyarp e2e log (e.g. two `200`s for the
      revoked-cert request, meaning the old code's no-CRL-check path silently accepted it) — confirmed via
      `docker image inspect dockyarp:local --format '{{.Created}}'` against the source files' mtimes, not
      guessed. Fixed by running `docker image build --tag dockyarp:local .` manually before re-testing; all
      three "bugs" evaporated on the very next run with zero further code changes — the mTLS/CRL implementation
      itself was already correct. 12/12 green.
- [x] 5.4 `docs/testing.md`: added the four new scenarios to the coverage map.

## 6. Documentation (AG-DOC)

- [x] 6.1 Updated both docs-site pages that reference this: `configuration.md` (new `ClientCrlPath` row in the
      `Tls` app-config table; `DOCKYARP_CLIENT_CERT` row expanded with the required/optional behavior split) and
      `features.md` (Mutual TLS bullet rewritten: CRL, optional's non-blocking behavior,
      SUCCESS/FAILED/NONE). `examples.md`'s `DOCKYARP_CLIENT_CERT: "required"` sample needed no change (still
      accurate). Grepped the whole `docs-site/` tree first per AGENTS.md's doc-audit habit — these were the only
      hits beyond the sample.
- [x] 6.2 `docs/labels-reference.md`: `DOCKYARP_CLIENT_CERT` row expanded with the same required/optional split.
      Also updated `docs/tls-acme.md`'s "Mutual TLS" section (internal architecture doc, described the old
      global/unconditional handshake behavior and the SUCCESS-only header — rewritten for the per-host
      handshake, CRL, and the three-value header contract).

## 7. Final validation (AG-AT)

- [x] 7.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 7.2 `./build.ps1 Test` — full unit + integration suite green, including all new tests from tasks 1, 2, 3, 4.
- [x] 7.3 `./build.ps1 E2E` — full e2e suite green (41/41, via the official pipeline which rebuilds the image
      automatically — no repeat of task 5's stale-image lesson), including the new scenarios from task 5; the
      existing `MutualTls_*` tests (Required-mode) pass unmodified (no regression from the per-host handshake
      change).
