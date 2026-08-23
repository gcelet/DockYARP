## Context

See proposal.md for motivation. Current code, read directly (not assumed from the backlog stub, which itself
flags this item as "more involved than the stub implies"):

- `ClientCertificateValidator.Validate(X509Certificate2)` (`src/DockYarp.Tls/`) builds an `X509Chain` against a
  **global** CA (`TlsOptions.ClientCaCertificatePath`, not per-host — DockYarp has no per-host client-CA
  mechanism, unlike nginx-proxy's `<host>.ca.crt`). No revocation check (`X509RevocationMode.NoCheck`).
- `SniTlsHandshakeCallback.BuildOptions(host)` sets `ClientCertificateRequired = true` and one shared
  `RemoteCertificateValidationCallback` whenever **any** client CA is configured (`mutualTls`), for **every**
  host, regardless of that host's own `ClientCertificateRequirement`. The callback already accepts a *missing*
  certificate (`null => true`) but rejects any *invalid* one (`Validate() == false`) — connection-wide, not
  per-host. This is why an `optional` host's invalid cert currently drops the connection instead of proceeding.
- `ClientCertificateMiddleware` only checks `route.ClientCertificate == Required && ClientCertificate is null` →
  403. This is currently *correct* only because the handshake already guarantees any present certificate is
  valid for every host uniformly — an assumption this change breaks for `Optional` hosts, so the middleware must
  be extended to distinguish presented-but-invalid from presented-and-valid.
- `ForwardedHeadersTransform` (`src/DockYarp.App/ReverseProxy/`) is a `static` request-transform lambda (no DI):
  it currently sets `X-SSL-Client-Verify: SUCCESS` whenever `Connection.ClientCertificate` is non-null, and no
  header at all otherwise — safe today only because an invalid cert never reaches this code (handshake already
  rejected it). Once `Optional` hosts can carry an untrusted/revoked certificate this far, that assumption also
  breaks: presence no longer implies success.
- `openspec/specs/yarp-dynamic-config/spec.md`'s "Forwarded headers" requirement explicitly states "when no
  client certificate is present, no `X-SSL-Client-*` header SHALL reach the backend" — the user chose to change
  this (add explicit `NONE`) rather than treat the backlog stub's "receives a header indicating no client cert"
  criterion as already satisfied by the header's absence.
- `LabelParser.TranslateNginxSslVerifyClient` already collapses nginx's `optional` and `optional_no_ca` to the
  same `ClientCertificateRequirement.Optional` value — this change's `Optional` handshake behavior (never fail,
  accept untrusted certs) matches nginx's `optional_no_ca` semantics specifically, which is the more permissive
  of the two and was already the implicit target per the backlog stub's own framing ("the non-blocking
  `optional_no_ca` behavior"). No further label-parsing change needed — this was decided when the two nginx
  values were originally collapsed.
- .NET (through .NET 10) has no public `X509Crl` type in `System.Security.Cryptography.X509Certificates` — no
  BCL CRL reader or writer. The user chose BouncyCastle (`BouncyCastle.Cryptography`, the actively maintained
  successor to `Portable.BouncyCastle`) over manual ASN.1 parsing (`System.Formats.Asn1`), since it also covers
  generating a test CRL fixture for unit tests (manual parsing would still need an external `openssl`-generated
  fixture, opaque and harder to vary across test cases).

## Goals / Non-Goals

**Goals:**
- CRL revocation checking, alongside the existing CA-chain check.
- A host's `optional` requirement genuinely never drops the TLS connection over an untrusted/revoked client
  certificate — the app (via the forwarded header) decides, matching nginx's `optional_no_ca`.
- `X-SSL-Client-Verify: SUCCESS|FAILED|NONE` on any `Required`/`Optional` route; DN headers only for `SUCCESS`.

**Non-Goals:**
- Per-host CA or per-host CRL (`<host>.crl.pem`) — both stay global-only, matching the existing (pre-this-change)
  global-only CA scope. Introducing per-host file mounts for client-cert config would be a separate, larger
  change with its own design questions (mount layout, reconciliation) not needed to satisfy this item's
  acceptance criteria.
- Changing `Required` hosts' behavior — a `Required` host keeps today's strict, connection-dropping validation
  (now also checking CRL); only `Optional` (and the newly-added `None`-skips-the-cert-prompt case) change.
- CRL freshness/auto-refresh (re-reading the CRL file on an interval, or fetching a CRL Distribution Point URL) —
  loaded once at startup like the CA, matching the existing `ClientCertificateValidator` lifecycle. A future item
  can add live reload if operators need it; out of scope here.

## Decisions

**`None` hosts stop requesting a client certificate at the TLS layer (a side effect of host-awareness, not
separately scoped work).**

Rationale: making `SniTlsHandshakeCallback.BuildOptions` resolve the host's requirement to pick a validation
callback naturally also lets it skip `ClientCertificateRequired` entirely for `None` — free correctness
improvement (closer to nginx's per-server-block `ssl_verify_client`, where an unconfigured vhost never prompts)
that falls directly out of the mechanism this item needs anyway for `Optional`, not scope creep.

**`DockYarp.Security` gains a `ProjectReference` to `DockYarp.Tls` (correction found during implementation, not
originally called out).**

Rationale: `ClientCertificateMiddleware` needs `ClientCertificateValidator` (CA-chain + CRL) to compute the
status for `Optional` routes, but `Security` previously only referenced `Core`. Confirmed safe (no cycle):
`Tls` only references `Core`, never `Security`. The alternative — capturing the outcome at the handshake layer
itself, inside `SniTlsHandshakeCallback`'s permissive `RemoteCertificateValidationCallback`, and passing it
downstream via a connection feature — was considered and rejected: that callback's signature
(`object, X509Certificate?, X509Chain?, SslPolicyErrors → bool`) has no connection-context/feature-collection
parameter to stash a result on, so correlating a callback invocation back to the right HTTP request would need
fragile ambient/async-local state. Reusing the validator directly in the middleware (see the next decision for
why that's not a hot-path cost) is simpler and no less correct.

**Verification status is computed once, in `ClientCertificateMiddleware`, and threaded to
`ForwardedHeadersTransform` via `HttpContext.Items` — not re-validated in the transform.**

Rationale: the transform runs per proxied request on the hot path (AGENTS.md's low-allocation guidance);
re-running `X509Chain.Build` (+ the new CRL check) there on every request would duplicate real crypto work
already done once in the middleware, which runs earlier in the same pipeline for every request regardless. The
transform becomes a pure read of a precomputed status — no new DI/service resolution needed in the (currently
`static`) transform lambda, since `HttpContext.Items` is already accessible from `TransformBuilderContext`.

**`X-SSL-Client-Verify` gains `NONE`; DN headers stay `SUCCESS`-only.**

Rationale: per the user's explicit choice — closes the ambiguity between "absent because no cert" and "absent
because this code path wasn't reached", and matches nginx's `$ssl_client_verify` more closely. DN headers are
withheld for `FAILED`/`NONE` because forwarding an unverified certificate's claimed subject/issuer as if
meaningful would be misleading to the backend (an untrusted cert's DN fields are attacker-controlled).

**BouncyCastle for CRL parsing, not manual ASN.1 — but `Portable.BouncyCastle`, not `BouncyCastle.Cryptography`
(correction found during implementation).**

Rationale: per the user's explicit choice — see Context. `ClientCertificateValidator`'s CRL check parses the
loaded PEM/DER CRL via BouncyCastle's `X509CrlParser`, extracts revoked serial numbers into a
`FrozenSet<System.Numerics.BigInteger>` at load time (once, like the CA), and checks the presented certificate's
serial against it per validation — no BouncyCastle type leaks past `ClientCertificateValidator`'s internals
(its public surface stays `X509Certificate2 → bool`, matching the existing signature).

The originally-planned modern package (`BouncyCastle.Cryptography`) turned out to conflict at compile time
(`CS0433`, ambiguous `Org.BouncyCastle.X509.X509Crl`/`X509CrlParser`): `Certes` (already a `DockYarp.Tls`
dependency, for ACME) transitively pulls `Portable.BouncyCastle` 1.9.0, whose assembly (`BouncyCastle.Crypto.dll`)
defines types in the exact same namespace as the newer package. Rather than fight the collision with an `extern
alias` (or worse, two BouncyCastle major versions loaded in one process), the fix was to use what's already
transitively present: `Portable.BouncyCastle` is now an explicit, pinned `PackageReference` (same version Certes
already needs), giving `DockYarp.Tls` direct access to its `X509CrlParser`/`X509Crl`/`X509CrlEntry` with zero new
assembly conflict. The only API-shape difference from the modern package: `X509Crl.GetRevokedCertificates()`
returns the older non-generic `Org.BouncyCastle.Utilities.Collections.ISet` (needs `.Cast<X509CrlEntry>()` before
any LINQ), and `X509Crl.NextUpdate` is a BC-specific `DateTimeObject` rather than `DateTime?` — neither is used
by this change's CRL-membership check, so no other code needed adjusting.

## Risks / Trade-offs

- [Risk] A new *direct* dependency (`Portable.BouncyCastle`) on the TLS-serving hot path — though not a new
  dependency to the process overall, since `Certes` already pulls it in transitively (see Decisions). →
  Mitigation: CRL parsing/lookup happens once at startup (like the CA) and per-validation is a simple set lookup,
  not a hot-path crypto operation; BouncyCastle is a long-standing, widely-used .NET crypto library, and this
  change pins explicitly a version already present rather than adding new supply-chain surface.
- [Risk] `HttpContext.Items` key collision if another middleware reuses the same key. → Mitigation: use a
  dedicated, namespaced key (e.g. a `static readonly object` sentinel, not a plain string) — the same pattern
  ASP.NET Core's own middleware uses internally to avoid string-key collisions.
- [Risk] Skipping the client-cert prompt for `None` hosts (see Decisions) could theoretically surprise an
  operator who relied on the old global-prompt behavior for some out-of-band reason. → Mitigation: assessed as
  low — the old behavior was never a documented feature (no spec requirement described "every host prompts for
  a client cert regardless of its own setting"), and TLS clients that don't have a certificate simply don't send
  one either way, so no client-visible failure mode changes for a `None` host.
