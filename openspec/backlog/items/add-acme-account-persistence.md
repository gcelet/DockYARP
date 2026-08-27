---
id: add-acme-account-persistence
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: high
status: backlog
nginx-proxy: acme-companion persists one ACME account (account.json under its own state volume), reused
  across every certificate it manages
provenance: 2026-08-27 — surfaced by add-acme-client-maintenance-policy's RFC 8555 audit, then correctly
  re-prioritized by the user: this doc first under-weighted the real severity by reasoning from a step-ca
  installation instead of DockYarp's actual target (a transparent nginx-proxy replacement, where Let's
  Encrypt — not step-ca — is the realistic default CA for most operators)
---

## Why

`AcmeClient` creates a brand-new ACME account on every single `RequestCertificateAsync` call — including
every renewal (every ~60 days per host by default, `RenewBeforeExpiry`). Against step-ca (self-hosted, no
default rate limits) this is mostly database clutter. **Against Let's Encrypt — the realistic default CA for
most nginx-proxy-replacement operators — this is a real production risk**: LE applies real per-account limits
(failed-validation and new-account-creation limits among them), and creating a throwaway account on every
renewal, across potentially many hosts over time, is exactly the pattern LE's own abuse detection exists to
flag. Certes had this same gap (a fresh `AcmeContext`/account key per call) — not a regression the hand-roll
introduced, but a pre-existing gap this audit surfaced.

**Real migration-continuity consequence, not just a rate-limit one**: an operator migrating from nginx-proxy
(whose `acme-companion` sidecar persists one ACME account across every certificate it manages) would have
DockYarp silently abandon that existing account relationship on day one, rather than continuing it — directly
contrary to DockYarp's own stated goal of being a transparent nginx-proxy replacement.

## nginx-proxy behavior

`acme-companion` (the sidecar nginx-proxy pairs with for ACME) persists a single ACME account
(`/etc/acme.sh`-style state, or its own equivalent) across every certificate it manages, reused for every new
order and every renewal — the conventional ACME client pattern (certbot, acme.sh, Certes' own typical usage
all do the same).

## DockYarp today

`AcmeClient.RequestCertificateAsync` (`src/DockYarp.Tls/AcmeClient.cs`) generates a fresh `ECDsa` account key
and calls `AcmeHttpClient.CreateAccountAsync` on every invocation — no persistence anywhere.

**Real technical path already found, don't re-derive**: RFC 8555's `newAccount` endpoint is idempotent by
design — if a request's JWK already has an associated account on the CA, the server returns the *existing*
account (200) instead of creating a new one (201). This means implementing persistence does **not** require
new "does an account already exist" lookup logic: generate the account key **once**, store it securely
(alongside `CertificateDirectory`, the same volume that already persists Data Protection keys), and reuse it
for every future `RequestCertificateAsync` call — `CreateAccountAsync`'s own call pattern stays unchanged,
only where the key comes from changes.

## Proposed change (sketch)

Not yet — real design questions to resolve, not assumed:
1. Where does the persisted account key live? (`CertificateDirectory`-adjacent, matching how certs/DP keys
   are already persisted on the same operator-mounted volume, is the obvious default — confirm no reason it
   shouldn't be.)
2. **Migration path**: can DockYarp *import* an existing nginx-proxy/acme-companion account key (so a
   migrating operator keeps their real, existing LE account rather than starting a new one that just happens
   to persist going forward)? Real investigation done (reading `acme.sh`'s own source, and cross-checked
   against a real operator installation):
   - acme.sh (what acme-companion wraps) stores the account key as a PEM private key, one per registered CA
     endpoint, under a path keyed by the CA's host+directory-path — confirmed both from source and from a
     real installation's on-disk layout.
   - **Key algorithm varies by operator choice, not a fixed format**: acme.sh's own default is RSA 2048
     unless the operator explicitly requested an EC key at registration time (`--accountkeylength ec-256` or
     similar) — confirmed from source. A real installation checked during this investigation turned out to
     be a 256-bit EC key (i.e. P-256, matching DockYarp's own ES256-only `AcmeClient` directly) — proving
     EC-keyed accounts do occur in practice, not just Certes'/DockYarp's convenience — but this is one data
     point, not a guarantee: an RSA-keyed account is plausibly just as common and DockYarp's client has no
     RS256 (or general JWS-algorithm-negotiation) support today.
   - **Realistic scope for this item**: support importing an EC (P-256) PEM account key directly — this
     covers a real, confirmed-to-exist subset of installations at low complexity. Treat RSA-keyed account
     import as explicitly out of scope for this item (would need RS256 JWS support added to `AcmeHttpClient`
     first) — worth its own follow-up item if there's real operator demand, not pre-built speculatively.
3. First-run behavior: if no persisted key exists yet (fresh install, not a migration), generate one and
   persist it immediately — this is the straightforward default case regardless of the migration question.
4. Once persisted, does `AcmeClient` gain the "reuse an already-`valid` authorization" optimization (RFC
   8555 §7.5) for a renewal within the CA's own authorization-reuse window, or is a fresh challenge always
   requested regardless? Real behavior difference worth deciding explicitly, not accidental.

## Acceptance criteria (→ scenarios)

TBD — depends on the design questions above. At minimum:
- **WHEN** DockYarp requests a second certificate (a different host, or a renewal) after its first ever ACME
  request **THEN** the same ACME account is reused, not a new one created (verifiable against a real CA by
  account ID/URL staying constant across requests).
- **WHEN** DockYarp starts fresh with no persisted account key **THEN** one is generated and persisted
  before the first ACME request.

## Notes / risks / references

- High priority: this is the one gap directly affecting DockYarp's core value proposition (a real,
  production-viable nginx-proxy replacement against the CA most operators actually use).
- Refs: `docs/tls-acme.md`'s "Client maintenance & security" section (where this was first documented, then
  corrected after user pushback on its original severity assessment),
  `openspec/changes/archive/2026-08-25-investigate-certes-aot-alternative/` (the original hand-roll, which
  carried this gap forward from Certes unnoticed at the time).
