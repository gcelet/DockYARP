---
id: fix-tls-chain-not-sent-in-handshake
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: high
status: backlog
nginx-proxy: (internal — DockYARP TLS correctness bug, no parity row)
provenance: 2026-08-17 user's real-world migration test on REDACTED — found immediately after
  fix-pem-cert-chain-dropped-on-load shipped, verified the loader fix alone doesn't resolve the live symptom
---

## Why
`fix-pem-cert-chain-dropped-on-load` (archived 2026-08-17) fixed `PemCertificateLoader.TryLoad` so it no longer
drops the intermediate certificate when parsing a multi-cert `.crt` file. **That fix is real and necessary, but
turned out to be insufficient on its own** — verified live against the user's real deployment (real step-ca
certificate, real leaf+intermediate `.crt` file confirmed to contain both blocks): the TLS handshake still sends
only the leaf certificate. `openssl s_client -showcerts` shows exactly one `BEGIN CERTIFICATE` block, and the
running container's own `/api/version` was cross-checked against `dotnet gitversion` on the exact fix commit
(`0.1.0-alpha.286`, matching precisely) — so this is not a stale-build or stale-container artifact, the fix is
genuinely live and the symptom persists anyway.

**Root cause, verified against Microsoft's own documentation, not assumed**: `SniTlsHandshakeCallback.BuildOptions`
(`src/DockYarp.Tls/SniTlsHandshakeCallback.cs:110`) sets `SslServerAuthenticationOptions.ServerCertificate` to a
bare `X509Certificate2` (from `SniCertificateSelector.Select`, which in turn comes from `ICertificateStore.Find`
— both typed as bare `X509Certificate2`/`X509Certificate2?`). Per Microsoft's TLS/SSL best-practices doc: **"When
the certificate is obtained by one of the other two ways [`ServerCertificate` or
`ServerCertificateSelectionCallback`, not `ServerCertificateContext`], a `SslStreamCertificateContext` instance
is created internally by the `SslStream` implementation"** — i.e. `SslStream` builds its own `X509Chain`
internally to decide what to send, using its own (system-store-dependent) chain-building logic, not necessarily
"whatever additional certificates happen to be bagged in the same PKCS12 the leaf was loaded from." The same doc
explicitly recommends the opposite of what DockYARP does: **"The recommended approach is to use
`SslServerAuthenticationOptions.ServerCertificateContext`"** — built explicitly via
`SslStreamCertificateContext.Create(certificate, additionalCertificates)`.

This means the *loading* side (fixed) and the *serving* side (still broken) are two separate bugs that happened
to produce the exact same external symptom, which is why the loader fix alone didn't resolve the live test.

## nginx-proxy behavior
N/A — internal DockYARP TLS-serving bug, not a proxy feature gap. No `parity.md` row.

## DockYarp today
- `src/DockYarp.Tls/ICertificateStore.cs`: `Find(string host)` returns a bare `X509Certificate2?` — no channel
  to carry "the intermediates that go with this leaf" separately.
- `src/DockYarp.Tls/SniCertificateSelector.cs`: `Select(string? host)` returns a bare `X509Certificate2`,
  built entirely on top of `ICertificateStore.Find`.
- `src/DockYarp.Tls/SniTlsHandshakeCallback.cs:110`: `ServerCertificate = selector.Select(host)` — the exact
  line that needs to become `ServerCertificateContext = ...` instead, per Microsoft's own recommendation.
- `FileCertificateStore.Load()` (fixed by `fix-pem-cert-chain-dropped-on-load`) now correctly builds a
  chain-inclusive `X509Certificate2` via a PKCS12 round-trip — but nothing downstream extracts the "additional
  certificates" back out of that object to build a proper `SslStreamCertificateContext`.
- `CertesAcmeClient.BuildPfx` (ACME-issued certs) has the *same* underlying shape (a chain-inclusive PFX-loaded
  `X509Certificate2`) — meaning **ACME-issued certificates may have exactly the same live handshake bug**, just
  never caught because there was no test asserting the wire-level chain, only that loading succeeded. This
  needs verifying, not assuming, once the fix is designed — a real regression risk if ACME certs currently "work"
  only because clients happen to already trust the intermediate some other way (e.g. Let's Encrypt's
  cross-signed/well-known intermediates being present in most system trust stores already, masking the bug for
  public CAs while a private CA like step-ca exposes it).

## Proposed change (sketch)
- Extend `ICertificateStore.Find` (or add a new method) to also expose the additional/intermediate certificates
  associated with a loaded certificate — needs a design decision: change the return shape (e.g. return a small
  record `(X509Certificate2 Leaf, X509Certificate2Collection Additional)`) vs. a parallel lookup. Consider using
  `X509CertificateLoader.LoadPkcs12Collection` (loads the *entire* PKCS12 bag as a collection) at the point
  certificates are loaded/reloaded, rather than trying to re-derive "what else was in the bag" from an already-
  loaded single `X509Certificate2` after the fact.
- Thread that through `SniCertificateSelector.Select` and into `SniTlsHandshakeCallback.BuildOptions`, replacing
  `ServerCertificate = ...` with `ServerCertificateContext = SslStreamCertificateContext.Create(leaf,
  additionalCertificates: intermediates)`.
- Verify (don't assume) whether `SslStreamCertificateContext.Create` needs `offline: true`/similar to avoid its
  own AIA/system-store fetch attempts — check current .NET docs at design time, this project's target version
  may differ from what's cited above.
- **Add real wire-level e2e test coverage — this is a required deliverable of this change, not optional
  follow-up.** Confirmed why no existing test caught this: `tests/DockYarp.E2E.Tests/TlsHarness.cs:167-173`'s
  `RemoteCertificateValidationCallback` **discards the `chain` parameter** (`(_, certificate, _, _) => ...`,
  the `X509Chain` is thrown away) and unconditionally `return true`s — it was never checking what got sent,
  only capturing the leaf. The fix needs a new (or extended) e2e test that:
  1. Does **not** discard `chain` — captures `chain.ChainElements.Count` and/or `chain.ChainStatus`.
  2. Configures the **client's own trust narrowly** (e.g. `SslClientAuthenticationOptions.CertificateChainPolicy`
     with `TrustMode = CustomRootTrust` and *only* step-ca's root in `CustomTrustStore`, revocation off) so the
     client cannot "cheat" by already having the intermediate in its own store — mirroring the same technique
     `CertificateStoreTests.ChainBuildsAgainst` already uses at the unit level (`fix-pem-cert-chain-dropped-on-load`),
     just now against the real live handshake instead of an in-memory loaded certificate.
  3. Asserts the chain actually contains the intermediate (not just that the connection succeeded — a lenient
     client could silently paper over a missing intermediate the same way the current harness does).
  This is exactly the layer the previous change's unit tests didn't reach, and exactly where this bug lives —
  a unit test proving "the loaded object carries the chain" cannot prove "Kestrel sent it," only a live
  handshake can.
- Re-verify ACME-issued (`CertesAcmeClient`) certificates against the same live-handshake test — confirm or
  rule out the same bug affecting them (see "DockYarp today" above). The e2e suite already provisions real
  step-ca certificates for several existing tests (`TlsTests.cs`, `RestartPersistenceTests.cs`) — extend one of
  those paths rather than standing up a new CA fixture.

## Acceptance criteria (→ scenarios)
- **WHEN** a host's certificate has an intermediate (PEM-provided or ACME-issued) **THEN** the TLS handshake
  actually sends the full chain — verified by an e2e test that inspects the live handshake's `X509Chain`
  against a client trusting only the root (not just that the loaded certificate object could theoretically
  build a chain in isolation).
- **WHEN** a host's certificate is a single leaf with no intermediate **THEN** behavior is unchanged (no
  regression for the already-working case).
- **WHEN** this change archives **THEN** the e2e suite has a test that would have failed against the
  pre-fix code (proven by the fact that today's existing e2e suite, with its trust-everything callback,
  does *not* fail despite the bug) — the new/extended test must be the kind that actually distinguishes
  "chain sent" from "chain not sent," not one that passes either way.

## Notes / risks / references
- Discovered immediately after `fix-pem-cert-chain-dropped-on-load` shipped and was verified live — the
  loader fix stays correct and should NOT be reverted; this is an additive follow-up, not a redo.
- Verified live on a real deployment (REDACTED, real step-ca-issued certs) before writing this stub — not
  from a lab reproduction alone.
- Refs: `src/DockYarp.Tls/SniTlsHandshakeCallback.cs:110`, `SniCertificateSelector.cs`, `ICertificateStore.cs`,
  `FileCertificateStore.cs`, `CertesAcmeClient.cs` (`BuildPfx`).
- Microsoft Learn: ".NET TLS/SSL best practices" — "Specify a server certificate" section (`ServerCertificate`
  vs `ServerCertificateContext`), consulted directly before writing this stub.
