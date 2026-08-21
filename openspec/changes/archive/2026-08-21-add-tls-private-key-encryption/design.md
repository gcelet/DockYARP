## Context

See `proposal.md` — Why, and the two design corrections found live in conversation before any code was
written (kept here since they materially shaped the final shape, not silently absorbed):

1. **Encrypting the TLS-serving key does not defend against the admin-dashboard-download threat model** —
   already reasoned through when this item was stubbed; restated because it shapes what this change does and
   does not claim.
2. **The re-encryption dashboard action must not be gated on a "previous passphrase" being configured.** The
   original sketch assumed the action was only needed for *rotation* (current + previous passphrase both set).
   The user's actual, immediate situation is a **first-time enable**: existing certificates with plain,
   unencrypted keys, no previous passphrase to speak of. Gating on `PreviousPrivateKeyEncryptionPassphrase`
   would have left that exact case with no explicit action at all. Corrected: gate on
   `PrivateKeyEncryptionPassphrase` being configured at all — covers both first-time enable and rotation with
   the same single condition.
3. **A per-host "needs re-encryption" indicator, added after the button/action itself was implemented.** The
   button originally rendered unconditionally on every row because the initial design assumed there was no
   cheap way to tell, from the dashboard, which host's key was already under the current passphrase without
   decrypting it. That assumption was wrong: `PemCertificateLoader` already determines, at load time, which
   passphrase tier (current, previous-fallback, or none/plain) decrypted a given key — the information was
   simply being discarded. Corrected: thread that signal out (see the new `PemLoadResult` type below) and
   surface it as a per-row badge next to the existing "Re-encrypt key" button, reusing the table's existing
   "near expiry" badge styling. Scope: a PFX-backed host is never flagged (`ConvertToPem` already applies the
   current passphrase when it rewrites the key, so there is nothing separate to flag there).

Current code, read directly, not re-derived:
- `LoadedCertificatePem.ExportPrivateKeyPem` (`src/DockYarp.Tls/LoadedCertificatePem.cs`) exports plain PKCS8
  PEM unconditionally, tries RSA then EC.
- `PemCertificateLoader.TryBuildKeyedChain`/`TryAttachPrivateKey` (`src/DockYarp.Tls/PemCertificateLoader.cs`)
  currently **re-attempts key import once per candidate certificate** in the chain (`TryAttachPrivateKey` is
  called inside `TryBuildKeyedChain`'s loop) — for a plain key this is cheap and harmless; for an *encrypted*
  key it would mean re-running password-based KDF decryption (deliberately slow, by design of PBE) once per
  candidate certificate, and — more importantly — makes "wrong password" and "wrong algorithm for this
  candidate" indistinguishable at exhaustion, since both currently surface as the same "return null, try next"
  outcome.
- `FileCertificateStore` (`Save`, `Load`, `ConvertToPem`) already holds a `TlsOptions options` field — the two
  new passphrase settings need no new constructor wiring, just reading off the existing `options`.

## Goals / Non-Goals

**Goals:**
- Optional at-rest encryption of the TLS-serving private key, default-off, zero behavior change when unset.
- Passphrase rotation that doesn't strand already-encrypted keys at the next restart.
- An explicit, dashboard-triggered way to force re-encryption onto the current passphrase — covering both
  first-time enable (plain → encrypted) and rotation (old passphrase → new passphrase) — without needing to
  distinguish those two cases in code.
- Decrypting a key must fail loudly and specifically, not be silently absorbed into the existing "this
  candidate doesn't match, try the next one" control flow that plain-key import already uses for algorithm
  mismatches.

**Non-Goals:**
- Any change to the admin-dashboard-download threat model — restated in Why, not solved here.
- Encrypting anything other than the TLS-serving certificate's own private key (Data Protection's key ring
  already has its own, separate mechanism).
- A generic multi-passphrase history (more than one "previous") — a single fallback covers the realistic
  rotation window (rotate, then re-encrypt everything via the dashboard action, then eventually drop the
  previous value from config); an unbounded list is unrequested complexity.

## Decisions

**Passphrase-based PKCS8 encryption (`ExportEncryptedPkcs8PrivateKeyPem`/`ImportFromEncryptedPem`), not a
wrapping certificate like `DataProtection:CertificatePath`'s mechanism.**

Rationale: `DataProtectionSetup`'s pattern (a PFX file as a wrapping key, via `ProtectKeysWithCertificate`) is
specific to the Data Protection subsystem's own key-ring API — there's no equivalent "protect an arbitrary PEM
private key with a wrapping certificate" primitive to reuse for `FileCertificateStore`. .NET's own
password-based PKCS8 encryption (`ExportEncryptedPkcs8PrivateKeyPem(password, PbeParameters)` /
`ImportFromEncryptedPem(pem, password)`, confirmed present on both `RSA` and `ECDsa` via Microsoft's own API
docs) directly matches what the user asked for — "une passphrase" — with no extra wrapping-certificate
artifact for the operator to generate and manage. `PbeParameters` fixed to AES-256-CBC + SHA-256 + 600,000
iterations (OWASP's current minimum recommendation for PBKDF2-HMAC-SHA256) as an internal constant — not
operator-configurable; exposing KDF tuning would be complexity with no realistic operator benefit here.

**Decide plain-vs-encrypted from the PEM's own label, never from whether encryption is configured.**

Rationale: this is what makes the feature genuinely zero-regression when off, and safe to turn on without
breaking operator-provided plain keys — matches the exact reasoning already applied to `IsPfxBacked`'s
filesystem-based (not config-based) detection in `change-cert-store-format-to-pem`/
`add-admin-dashboard-cert-conversion`. Checking for the literal `ENCRYPTED PRIVATE KEY` substring in the PEM
text is a direct, RFC-7468-label-based check — no ambiguity.

**Restructure `PemCertificateLoader` to decrypt/import the key exactly once, then try it against each candidate
certificate — not re-attempt import per candidate.**

Rationale: for a plain key this is a harmless simplification; for an *encrypted* key it avoids re-running
deliberately-slow PBE key derivation once per chain member, and — the real requirement — lets decryption
failure be handled as its own distinct, loud outcome (an actionable exception naming the host/file) rather than
being absorbed into the existing "try RSA, then EC, then give up quietly" loop that's correct for algorithm
mismatches but wrong for a bad passphrase. Concretely: decrypt/import once into an `RSA`/`ECDsa` instance
(trying current passphrase, then previous, throwing if both fail for an encrypted key; trying plain RSA-then-EC
import unchanged for a non-encrypted key), then loop candidates only for `CopyWithPrivateKey`'s own
public-key-match check (`ArgumentException` = doesn't match this candidate, try next — unrelated to
decryption).

**`PemCertificateLoader.TryLoad` reports which passphrase tier decrypted the key, via a new `PemLoadResult`
type, instead of only reporting the loaded certificate.**

Rationale: this is the load-time signal design correction #3 above depends on. `PemLoadResult` is a
property-initialized record (not a positional one) specifically to keep its `bool RequiresReencryption`
property out of a constructor parameter list — a bare `bool` constructor parameter trips this project's
AV1564 analyzer (unclear at the call site what `true`/`false` means), which a named object-initializer property
does not. `FileCertificateStore` tracks the per-host flag in a small `HashSet<string>` alongside its existing
`certificates` dictionary, set during `Load()` and cleared by `Save()`/`ConvertToPem()`/`ReencryptPrivateKey()`
— whichever of those three actually rewrites `.key` under the current passphrase.

**The re-encryption action reuses the exact same POST-handler/anti-forgery/opt-in shape as
`add-admin-dashboard-cert-conversion`'s conversion action, as a *second*, separately-named method — not a
rename or repurposing of `ConvertToPem`.**

Rationale: `ConvertToPem`'s existing contract ("rewrites a `.pfx`-backed host") stays accurate and unchanged;
`ReencryptPrivateKey` is a new, separately-scoped operation (rewrites `.key` under current passphrase settings,
regardless of the host's current format) that happens to share nearly identical write-side mechanics. A small
amount of duplication between the two methods is an accepted, deliberate trade-off over either renaming the
shipped, spec'd, tested `ConvertToPem` (real churn across five files plus the archived spec) or overloading its
meaning to cover a case its name and docs don't describe.

## Risks / Trade-offs

- [Risk] An operator loses/forgets the passphrase entirely (no previous, no current match). → Accepted:
  matches `DataProtectionSetup`'s own precedent (`LoadEncryptionCertificate` throws `InvalidOperationException`
  with an actionable message) — fail fast at startup naming the host/file, not a silent fallback to serving
  without that host's certificate.
- [Risk] `PreviousPrivateKeyEncryptionPassphrase` left configured indefinitely after a rotation is "done" keeps
  the re-encryption action visible for every host forever, which is mildly confusing UI noise but not unsafe
  (the action is idempotent-ish — re-encrypting an already-correctly-encrypted key just rewrites the same
  content). → Accepted, not solved here: operators are expected to unset it once rotation is confirmed
  complete, matching how they'd retire any rotated secret.
- [Risk] The `PemCertificateLoader` restructuring touches a working, tested, spec'd code path
  (`fix-pem-cert-chain-dropped-on-load`/`fix-tls-chain-not-sent-in-handshake`'s history). → Mitigation: the
  existing chain-preservation/reversed-order/mismatched-key test coverage in
  `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs` must stay green unmodified through this refactor — that
  suite is the regression guard, not something this change should need to weaken to pass.
