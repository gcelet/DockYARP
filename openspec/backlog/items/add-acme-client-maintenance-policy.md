---
id: add-acme-client-maintenance-policy
capability: tls-acme
agent: AG-AT
tier: C-doc
priority: medium
status: backlog
nginx-proxy: n/a (internal finding — process/documentation, not a parity gap)
provenance: 2026-08-26 — user request following investigate-certes-aot-alternative: a hand-rolled ACME v2
  client is a real ongoing maintenance/security surface, and the user's stated default preference is a
  maintained NuGet package over self-maintained protocol code, overridden here only because no AOT-clean,
  trustworthy package existed at the time
---

## Why

`investigate-certes-aot-alternative` replaced Certes with a ~350 LOC hand-rolled ACME v2 (RFC 8555) client
because no NuGet candidate was both AOT-clean and trustworthy. That decision trades one problem (Newtonsoft
dependency) for another (DockYarp now owns correctness/security maintenance for a protocol client it used to
get from a package). `docs/tls-acme.md` — the existing architecture reference for this whole area — is also
now **stale**: it still names `CertesAcmeClient` twice (component table + testing-boundary section).

## nginx-proxy behavior

N/A — internal process/documentation, not a proxy-behavior parity gap.

## DockYarp today

`docs/tls-acme.md` documents the TLS/ACME architecture but has no section on the hand-roll's maintenance
posture, and its 2 `CertesAcmeClient` references are stale since that class was renamed to `AcmeClient`.

**Real RFC 8555 completeness audit already done, don't re-derive** (read directly against
`src/DockYarp.Tls/Acme/AcmeHttpClient.cs` and `AcmeClient.cs`):
- **Real gap, security-relevant**: no certificate revocation (§7.6) — no automated ACME-based path to revoke
  a certificate if its private key were compromised.
- **Real gap, reliability**: no `Retry-After`-aware backoff on rate-limit (`rateLimited`) or other transient
  errors — only `badNonce` (§6.7) gets a bounded retry today.
- **Not gaps, structurally moot given the design**: account update/deactivation (§7.3.2/§7.3.6), account key
  rollover (§7.3.5), pre-authorization (§7.4.1), and reusing an already-`valid` authorization from a prior
  order — all require a *persisted* ACME account across requests; DockYarp creates a fresh account key every
  single `RequestCertificateAsync` call, so the CA never has anything to reuse or roll over.
- TLS-ALPN-01 challenge type: not implemented, not needed (DockYarp doesn't offer that challenge path).
- ACME Renewal Info (ARI, a newer draft extension, not core RFC 8555): not implemented — a real future-watch
  item, not a current gap (Certes itself predates ARI too).

## Proposed change (sketch)

1. Fix `docs/tls-acme.md`'s 2 stale `CertesAcmeClient` references (→ `AcmeClient`).
2. Add a new section to `docs/tls-acme.md` (a standing reference doc, same pattern as `docs/aot-readiness.md`
   — updated in place by future changes, not re-archived) covering:
   - The hand-roll's real scope (RFC 8555 subset implemented) and the audit findings above, verbatim —
     including the 2 real gaps and why the rest are structurally not applicable.
   - Explicit "when to reconsider" triggers: a maintained, AOT-clean NuGet package appears (re-check the 3
     forks + 2 leads already investigated, don't just assume the landscape is unchanged); a security advisory
     affects ACME client implementations generally; DockYarp's own requirements grow beyond the current
     scope (e.g., needing revocation for real).
   - A one-line pointer to `openspec/changes/archive/2026-08-25-investigate-certes-aot-alternative/` for the
     full original research, so this doc stays a summary, not a duplicate.
3. Open a separate follow-up backlog item for certificate revocation (the one real security-relevant gap) —
   do not implement it as part of this item, which stays documentation-only.

## Acceptance criteria (→ scenarios)

- **WHEN** `docs/tls-acme.md` is read **THEN** it accurately names `AcmeClient`, not `CertesAcmeClient`,
  everywhere.
- **WHEN** a future session considers touching ACME client code **THEN** the doc gives them the real
  known-gaps list and revisit triggers without re-deriving the audit.

## Notes / risks / references

- This item is documentation-only by design — see "Proposed change" step 3 for how the one real
  security-relevant finding (revocation) gets tracked instead of silently fixed here.
- Refs: `openspec/changes/archive/2026-08-25-investigate-certes-aot-alternative/` (the original research and
  hand-roll design), `docs/aot-readiness.md` (the standing-doc pattern this follows).
