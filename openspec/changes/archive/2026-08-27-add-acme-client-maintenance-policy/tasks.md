## 1. Fix stale references and document the maintenance policy (AG-AT)

- [x] 1.1 Fixed `docs/tls-acme.md`'s 2 stale `CertesAcmeClient` references (component table + testing-boundary
      section) to `AcmeClient`.
- [x] 1.2 Added a "Client maintenance & security" section to `docs/tls-acme.md` covering: the hand-roll's real
      RFC 8555 scope, the 2 real gaps found (revocation, rate-limit backoff) and why the other candidate
      gaps are structurally moot given the fresh-account-per-request design, and explicit triggers for
      reconsidering a NuGet package.

## 2. Track the real follow-up (AG-AT)

- [x] 2.1 Created `openspec/backlog/items/add-acme-certificate-revocation.md` for the security-relevant gap
      (RFC 8555 §7.6), scoped as its own future item — no implementation here.
- [x] 2.2 Created `openspec/backlog/items/add-acme-account-persistence.md` (high priority) and
      `openspec/backlog/items/add-acme-retry-after-backoff.md` — added after user pushback corrected this
      change's original under-weighted severity assessment (see design.md's Decisions). Both scoped as their
      own future items, no implementation here.

## 3. Verify (AG-AT)

- [x] 3.1 Grepped `docs/tls-acme.md` for `CertesAcmeClient` — zero remaining hits.
