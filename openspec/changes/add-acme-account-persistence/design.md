## Context

See `proposal.md` - Why. `AcmeClient.RequestCertificateAsync` (`src/DockYarp.Tls/AcmeClient.cs`) currently
creates a fresh `ECDsa accountKey` locally on every call and passes it to a fresh `AcmeHttpClient`, which
calls `CreateAccountAsync` unconditionally. `AcmeClient` is registered as an `AddSingleton<IAcmeClient,
AcmeClient>` (`TlsServiceCollectionExtensions.cs`), constructed once at startup, but its account key today is
call-scoped, not instance-scoped.

`CertificateProvisioningService` invokes `RequestCertificateAsync` concurrently across hosts with bounded
parallelism (`tls-acme`'s "Resilient concurrent provisioning" requirement) — any account-key sourcing change
must stay correct under that concurrency, not just under a single sequential call.

Real-world reference (from investigating an operator's actual acme-companion installation, see
`docs/tls-acme.md`): acme.sh persists one PEM private key per CA endpoint, EC or RSA depending on what was
requested at registration time (RSA 2048 is acme.sh's own default when no EC key length is explicitly
passed).

## Goals / Non-Goals

**Goals:**
- Persist DockYarp's own ACME account key across process restarts and across every `RequestCertificateAsync`
  call, relying on RFC 8555 `newAccount` idempotency to avoid duplicate-account creation.
- Let an operator import an existing **EC (P-256)** account key so a migrated installation continues an
  existing CA account relationship instead of starting a new one.
- Fail clearly, not silently, when the persisted-key location holds something DockYarp can't use (wrong
  algorithm, corrupt file).

**Non-Goals:**
- RSA (or any non-EC) account key import — out of scope; would require adding RS256 (or general
  JWS-algorithm-negotiation) support to `AcmeHttpClient`/`AcmeJws`, a separate, larger change with its own
  cost/benefit case. Track as a future item only if real operator demand shows up, not pre-built here.
- The RFC 8555 §7.5 "reuse an already-`valid` authorization" optimization. Persisting the account is a
  prerequisite for that optimization to make sense, but this change does not implement it — a fresh challenge
  is still requested for every order, same as today. Worth a follow-up once this ships and the actual renewal
  behavior under a persisted account is observed.
- Account update/deactivation/key-rollover (RFC 8555 §7.3.2/.5/.6) — no operator-facing need identified yet;
  revisit only if one surfaces.

## Decisions

**Where the key lives**: `{CertificateDirectory}/acme/{contact-email}/{directory-host}/{directory-path}/account.key`
— scoped per **(contact email, ACME directory endpoint)** pair, not just per endpoint. Reconsidered twice
during review:

1. First pass: flat, single file regardless of endpoint or contact. Rejected — see below.
2. Second pass: nested by endpoint only (`acme/{directory-host}/{directory-path}/account.key`), mirroring
   acme.sh's per-CA nesting. Still wrong: DockYarp already supports a **per-host contact email**
   (`LETSENCRYPT_EMAIL`, flowing through `DesiredCertificate.Email` into `RequestCertificateAsync`'s `email`
   parameter — `CertificateProvisioningService.cs`). RFC 8555 `newAccount` resolves the account by the
   request's **JWK**, not its `Contact` field — so reusing one persisted key across every host regardless of
   that host's own declared email would silently attach only the *first* host's email to the real account,
   and every other host's distinct email would never actually reach the CA. That's a real functional
   regression against today's behavior (a fresh account per request currently *does* honor each host's own
   email) and diverges from the real acme-companion installation investigated for this change, whose actual
   on-disk layout nests by account identity (an email-named folder) *before* the per-CA `ca/<host>/<path>/`
   segment — precisely so that different `LETSENCRYPT_EMAIL` values get separate, independent accounts.
3. **Final**: nest by contact email first, then by CA endpoint (matching the real installation's own
   ordering), so hosts sharing a contact email share one persisted account (achieving this change's actual
   goal — avoiding a throwaway account per renewal against Let's Encrypt) while hosts declaring *different*
   emails still get independent accounts, exactly as today. A host with no explicit email falls back to
   `Tls:ContactEmail` (unchanged existing resolution), which then also becomes its scoping key.

Not directly in `CertificateDirectory` alongside `{host}.crt`/`{host}.key` pairs, to avoid ever colliding with
a real host literally named after an email or CA host; mirrors the existing `dataprotection-keys` subfolder
convention (`DataProtectionSetup.cs`) for the same reason. No new `TlsOptions` setting — the path is derived
from the resolved contact email and `AcmeDirectoryUri`, not independently configurable. The email segment is
used verbatim as a folder name (acme.sh does the same); no additional sanitization is applied beyond what
filesystem path construction already requires, since a contact email's character set does not include path
separators.

**How the key is sourced (thread-safety under concurrent provisioning)**: keep `RequestCertificateAsync`'s
existing shape — a fresh `ECDsa` instance is still created for *each* call — but the key *material* it's
initialized from is now loaded from the persisted PEM (generating and persisting it first, if absent) instead
of `ECDsa.Create(ECCurve.NamedCurves.nistP256)`'s random generation. This sidesteps sharing one mutable
`ECDsa` object across concurrently-executing provisioning calls (`ECDsa`'s thread-safety under concurrent use
is not documented/guaranteed) while still achieving the actual goal: every call signs with the *same key
content*, so the CA's `newAccount` idempotency resolves them all to the same account. The persisted-PEM
read is cheap relative to the network round trips already dominating this method.

**Import format**: a PEM-encoded EC private key at the persisted-key path, either PKCS8 (`PRIVATE KEY`) or
SEC1 (`EC PRIVATE KEY`) form — `ECDsa.ImportFromPem` accepts both, so an operator can drop in whatever form
their prior client produced without a manual conversion step.

**Unsupported-algorithm detection**: if `ECDsa.ImportFromPem` fails on the persisted-key file, attempt
`RSA.ImportFromPem` purely to produce a precise error ("found an RSA key, only EC P-256 is supported for
import") instead of a generic parse failure — operators migrating from acme.sh's RSA-by-default accounts are
the expected case that hits this, and the error should tell them why import isn't possible, not just that it
failed.

## Risks / Trade-offs

- **[Risk]** Every host sharing a contact email (and CA endpoint) now shares one ACME account; if that
  account is ever rate-limited or flagged by the CA, every host under that email is affected simultaneously,
  not just one. **Mitigation**: this matches nginx-proxy/acme-companion's own real-world behavior exactly
  (one account per registered contact, reused across everything under it) — not a regression DockYarp
  introduces, and fewer `newAccount` calls overall is the actual improvement this change makes over today's
  per-request-account behavior. Hosts declaring distinct contact emails remain fully isolated from each
  other, unchanged from today.
- **[Risk]** A corrupted or unparseable persisted account-key file now blocks provisioning for every host, not
  just one. **Mitigation**: fail fast with a clear, actionable error identifying the file and the problem —
  same posture already established for other unloadable key material (`FileCertificateStore`'s encrypted-key
  error handling).
- **[Risk]** A long-lived account private key is marginally more sensitive than a discarded-after-use one.
  **Mitigation**: stored alongside certificate private keys and Data Protection keys already on the same
  operator-mounted volume, under the same existing trust assumption — not a new trust boundary.

## Migration Plan

Not a breaking change. A fresh install with no persisted account key generates and persists one on first
use — identical wire behavior to today except the key is kept instead of discarded. No data migration step is
required for a non-migrating operator. An operator migrating an EC-keyed existing account places that PEM
file at the persisted-key path before first use (see `specs/tls-acme/spec.md`'s "ACME account import"
requirement). Rollback (reverting this change) is safe: the persisted key file simply becomes unused again,
harmless to leave in place.
