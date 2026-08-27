## Context

See `proposal.md` and the backlog item's own "DockYarp today" section for the full real audit findings —
not repeated here.

## Goals / Non-Goals

**Goals:**
- Give a future session (or contributor) the real known-gaps list and revisit triggers without re-deriving
  the RFC 8555 audit.
- Keep the doc a living reference, updated in place by future changes — same pattern as
  `docs/aot-readiness.md`, not a one-off artifact that goes stale again.

**Non-Goals:**
- Not implementing any of the found gaps here (revocation, account persistence, retry-after backoff) — each
  tracked as its own follow-up backlog item, this change stays documentation-only.

## Decisions

**One new section in `docs/tls-acme.md`, not a new file.** That doc is already the authoritative TLS/ACME
architecture reference and already has a "Testing boundary" section covering the client's own limits — a
"Client maintenance & security" section belongs next to it, not in a separate document that could drift out
of sync or go undiscovered.

**Every real gap gets its own backlog item — corrected mid-change after user pushback.** The first pass at
this section reasoned about severity from a step-ca installation (no default rate limits) and concluded
account-persistence and retry-after backoff were low-priority doc notes, not real items. The user correctly
pushed back: DockYarp's actual goal is a transparent nginx-proxy replacement, where **Let's Encrypt — not
step-ca — is the realistic default CA** for most operators, and Let's Encrypt's real per-account rate limits
plus migration-continuity concerns (an operator moving from nginx-proxy's account-persisting acme-companion)
make both gaps genuinely significant, not niceties. Revised: `add-acme-account-persistence` (high priority —
the most significant of the three, directly affecting the core value proposition),
`add-acme-certificate-revocation`, and `add-acme-retry-after-backoff` are all real, separately tracked items.

## Risks / Trade-offs

- [Risk] The doc could itself go stale again if a future ACME-touching change doesn't update it →
  Mitigation: same risk `docs/aot-readiness.md` already carries and manages via `AGENTS.md`'s own periodic
  doc-audit habit — no new mechanism needed, just consistent practice.
