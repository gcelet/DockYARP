## Context

See `proposal.md` - Why for the root-cause analysis (verified against Microsoft's TLS/SSL best-practices
documentation, reproduced live). This section covers the concrete implementation shape, verified against the
actual .NET APIs via `dotnet-inspect` before committing to it — not assumed.

## Goals / Non-Goals

**Goals:**
- The TLS handshake actually sends the full chain for any certificate that has one — PEM-provided, PFX-provided,
  or ACME-issued — not just "the loaded object theoretically could build a chain."
- A single, consistent shape (`LoadedCertificate`) carries "leaf + additional certificates" from load/issuance
  through storage to serving, so the three previously-independent `LoadPkcs12`-single call sites can't silently
  diverge again.
- Real e2e coverage that would have failed against the pre-fix code, closing the gap that let this bug ship
  undetected the first time.

**Non-Goals:**
- Not re-validating or re-ordering the chain (leaf → intermediate → ... → root wire order is `SslStream`'s own
  concern once given the right certificates via `SslStreamCertificateContext`) — this change only ensures the
  right certificates are actually handed over, not how the handshake orders them.
- Not adding certificate revocation, OCSP, or trust validation beyond what already exists.
- Not touching `DefaultCertificateProvider`'s self-signed generation path (`DefaultCertificateFactory`) — it has
  no chain by construction; it only needs the same `LoadedCertificate` *shape* so it composes with the rest of
  the pipeline, not new behavior.

## Decisions

- **`ICertificateStore.Find`'s return type changes to `LoadedCertificate?`** (a record with `Leaf` and
  `Additional`), rather than adding a parallel `FindAdditional` method — confirmed with the user: changing the
  return shape makes it impossible to accidentally use a leaf without its chain, at the cost of touching every
  existing caller. Given the caller set is small and internal to `DockYarp.Tls` (`SniCertificateSelector`,
  `DefaultCertificateProvider`, test fixtures), this cost is low and the correctness benefit is worth it.
- **`X509CertificateLoader.LoadPkcs12Collection` (not `LoadPkcs12`) at all three load/issuance sites.** Verified
  via `dotnet-inspect` that this method exists (`LoadPkcs12Collection(byte[] data, string? password, ...)`,
  returns `X509Certificate2Collection`) and is documented as loading "a collection of all of the certificates"
  in a PKCS12 — exactly what's needed to stop silently dropping bagged certificates. The leaf within the
  returned collection is identified by `HasPrivateKey`, not by position (mirrors the same "don't assume file
  order" decision `fix-pem-cert-chain-dropped-on-load` already made for PEM parsing).
- **`PemCertificateLoader.TryLoad` changes to build and return the full collection**, not round-trip to a single
  `X509Certificate2` — its existing PKCS12 round-trip (added by the previous change) becomes the *input* to
  `LoadPkcs12Collection` instead of `LoadPkcs12`, so the two loading paths (`.crt`/`.key` and `.pfx`) converge on
  the same collection-based shape in `FileCertificateStore.Load()`, rather than one path (PFX) still silently
  losing the chain while the other (PEM) doesn't.
- **`SslStreamCertificateContext.Create(leaf, additionalCertificates)` replaces the bare `ServerCertificate`
  assignment** in `SniTlsHandshakeCallback.BuildOptions` — the one line Microsoft's own docs identify as the
  fix. Whether `Create` needs any extra flag to avoid its own AIA/system-store fetch attempts (relevant given
  .NET 11's AIA-download-disabled-by-default change cited in research) needs a final check against the actual
  target framework version at implementation time — a small, contained verification, not a design fork.
- **`IAcmeClient.RequestCertificateAsync` and `ICertificateStore.Save` both change to use `LoadedCertificate`
  too**, not just `Find` — confirmed necessary (not just for symmetry) because `CertesAcmeClient.cs:42` was
  independently found to drop the chain via the same single-cert `LoadPkcs12` pattern, before the certificate
  ever reaches `Save`. Fixing only `Find` would leave ACME-issued certificates broken on disk even after a
  correct in-memory representation existed at issuance time.

## Risks / Trade-offs

- [Risk] This is a wider-than-originally-scoped change (three call sites, four interface members) touching a
  hot path (TLS handshake) → [Mitigation] each call site's fix is mechanically the same substitution
  (`LoadPkcs12` → `LoadPkcs12Collection` + partition by `HasPrivateKey`), low conceptual risk despite the
  file-count; full non-E2E suite plus the new e2e wire-level test gate this before archiving.
- [Risk] `SslStreamCertificateContext` creation is documented as "CPU intensive" (builds an `X509Chain`
  internally) and Microsoft recommends caching/reusing instances across handshakes rather than rebuilding per
  connection → [Mitigation] `SniCertificateSelector`/`SniTlsHandshakeCallback` already re-resolve per handshake
  today (no existing caching layer for the bare `X509Certificate2` either), so this isn't a new perf
  regression relative to today's behavior — but worth a follow-up note if a caching layer is added later
  (out of scope here; not blocking this correctness fix).
- [Risk] Real e2e coverage for this is exactly the layer that previously let the bug ship silently →
  [Mitigation] the new/extended `TlsHarness.cs` test explicitly captures and asserts on `X509Chain`, not just
  connection success — designed specifically to fail against the pre-fix behavior (per the proposal's
  acceptance criteria).
