## Context

See `proposal.md` for the full candidate-elimination research. The real Certes usage surface (read from
`src/DockYarp.Tls/CertesAcmeClient.cs`, the only file touching `Certes.*`) is: account creation, single-host
order creation, HTTP-01 or DNS-01 challenge retrieval + trigger, authorization-status polling (already
hand-rolled), CSR generation from a fresh ES256 key, issued-chain retrieval. No account persistence, no
revocation, no multi-domain orders, no RSA support anywhere.

**A directly relevant, hard-won lesson from this exact codebase** (`fix-adminapi-json-aot-trim`): registering
a `JsonSerializerContext` via DI does **not** make the AOT/trim analyzer treat a `JsonSerializer.
Deserialize<T>`/serialize call site as trim-safe — the analyzer flags based on which overload the compiler
statically bound to, not what options a resolver chain supplies at runtime. The context must be passed
**explicitly at each call site** (`JsonSerializer.Deserialize(json, AcmeJsonContext.Default.Order)`, not
`JsonSerializer.Deserialize<Order>(json)`). This applies directly here: the whole point of this change is
AOT-cleanliness, so every JSON (de)serialization in the new ACME client must go through an explicit
`JsonSerializerContext`, not ambient `JsonSerializerOptions`.

## Goals / Non-Goals

**Goals:**
- Remove `Certes` (and therefore `Newtonsoft.Json`) from `DockYarp.Tls`'s dependency graph entirely.
- Preserve the `IAcmeClient` contract and all existing behavior exactly: HTTP-01 and DNS-01 challenges,
  chain-building (leaf + every returned issuer, no self-signed-root PKIX requirement — this was itself a
  real bug fix in Certes' own default mode, already worked around in the current code's own remarks), the
  existing 30×2s authorization poll.
- Zero new third-party dependencies — BCL (`System.Security.Cryptography`, `System.Text.Json`) only.

**Non-Goals:**
- Not implementing RSA/other JWS algorithms — `KeyAlgorithm.ES256` is the only one ever used.
- Not implementing multi-domain (SAN) orders — `DesiredCertificate` is one host at a time.
- Not implementing ACME account persistence, revocation, external account binding, CAA, or ARI
  (renewal-info) — none of these are exercised today.
- Not changing `AcmeChallengeType`, `DesiredCertificate`, `CertificateProvisioningService`, or any DI wiring
  beyond the one line registering the concrete client type.

## Decisions

**File breakdown** (all under `src/DockYarp.Tls/Acme/` except the renamed top-level client):

- `AcmeClient.cs` (renamed from `CertesAcmeClient.cs` — the name is misleading once Certes is gone):
  orchestrates `RequestCertificateAsync`/`CompleteHttpChallengeAsync`/`CompleteDnsChallengeAsync`/
  `WaitForValidationAsync`/`BuildLoadedCertificate`, same shape as today, calling into `AcmeHttpClient`
  instead of `Certes.Acme` types. `TlsServiceCollectionExtensions.cs`'s DI registration updated to match.
- `Acme/AcmeHttpClient.cs`: directory discovery (cached per instance — one per `RequestCertificateAsync`
  call, matching today's fresh-`AcmeContext`-per-call behavior), `Replay-Nonce` tracking, JWS request
  assembly + signing (delegates to `AcmeJws`), POST and POST-as-GET helpers, one bounded retry on a
  `badNonce` `problem+json` error (RFC 8555 §6.7 — the response itself carries a fresh nonce to retry with).
- `Acme/AcmeJws.cs`: static helpers — JWK construction from an `ECDsa` public key (`{kty: "EC", crv:
  "P-256", x, y}`, RFC 7518 §6.2.1 field order), RFC 7638 JWK thumbprint (canonical JSON → SHA-256 →
  base64url), and JWS assembly (protected header + payload, base64url each, signed via
  `ECDsa.SignData(ReadOnlySpan<byte>, HashAlgorithmName.SHA256, DSASignatureFormat.
  IeeeP1363FixedFieldConcatenation)` — confirmed via `dotnet-inspect` against the real BCL: this signature
  format produces the raw `r‖s` concatenation JWS ES256 requires directly, no DER-to-raw conversion needed).
- `Acme/AcmeModels.cs`: record types for the JSON shapes actually exchanged (`AcmeDirectory`, `AcmeAccount`,
  `AcmeOrder`, `AcmeAuthorization`, `AcmeChallenge`, `AcmeProblemDetails`) plus `AcmeJsonContext : 
  JsonSerializerContext` (`[JsonSerializable(typeof(AcmeOrder))]` etc. for every type) — passed explicitly at
  every `JsonSerializer.Serialize`/`Deserialize` call site per the Context section above.

**CSR generation via `CertificateRequest`, not hand-rolled ASN.1.** `new CertificateRequest($"CN={host}",
ecdsaKey, HashAlgorithmName.SHA256).CreateSigningRequest()` — confirmed real via `dotnet-inspect` — produces
the DER-encoded PKCS#10 CSR directly; base64url-encode for the finalize request body. No new crypto code
needed beyond what the BCL already provides.

**Issued-chain retrieval via `X509Certificate2Collection.ImportFromPem`, not manual DER parsing.** The
`certificate` endpoint returns `application/pem-certificate-chain` (leaf + intermediates concatenated PEM) —
confirmed real via `dotnet-inspect`: `ImportFromPem(ReadOnlySpan<char>)` parses a multi-certificate PEM
directly into a collection, actually simpler than Certes' own `CertificateChain.Certificate`/`.Issuers`
DER-based split that `BuildLoadedCertificate` currently has to reassemble by hand. `BuildLoadedCertificate`
itself is kept, adapted to take the imported collection instead of a `CertificateChain`.

**Alternative considered — reuse `Anvil`'s (Candidate C) source code directly, stripped of Newtonsoft**:
rejected. Its JWS/JWK/directory code is intertwined with its own `Newtonsoft.Json`-based `JsonUtil` and
broader multi-algorithm (RSA+EC), multi-order feature surface throughout — extracting just the needed
ES256-only, single-order subset would mean rewriting most of the touched files anyway, with the added cost
of tracking a codebase not designed for this narrower use, for no real benefit over writing directly against
the concrete 9-point surface already known.

## Risks / Trade-offs

- [Risk] A hand-rolled JWS/ACME implementation could have a subtle protocol bug a mature library would have
  already caught → Mitigation: the existing E2E suite already exercises both HTTP-01 and DNS-01 against a
  real step-ca test authority (`TlsTests.cs`) — this is real protocol-level proof, not just unit tests; unit
  tests additionally cover JWS structure, JWK thumbprint (self-consistency, mirroring
  `Rfc2136DnsChallengeProviderTests`'s own approach for the DNS-01 hand-roll), and CSR building in isolation.
- [Risk] `badNonce` retry logic, if done sloppily, could loop or fail silently → Mitigation: bounded to one
  retry (RFC 8555 §6.7's own documented pattern — a fresh nonce always accompanies the error response), not
  unbounded.
- [Risk] Losing Certes' ecosystem maintenance (security patches to the protocol implementation itself) →
  Mitigation: the surface is narrow enough to review as a whole, and this mirrors the project's own already-
  successful `add-acme-dns01` precedent (hand-rolled RFC 2136 DNS UPDATE + TSIG) for exactly this class of
  "no trustworthy package exists" situation.
