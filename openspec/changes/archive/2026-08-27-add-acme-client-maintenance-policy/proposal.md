## Why

`investigate-certes-aot-alternative` replaced Certes with a ~350 LOC hand-rolled ACME v2 (RFC 8555) client
because no NuGet candidate was both AOT-clean and trustworthy. That trades one problem (a Newtonsoft
dependency) for another (DockYarp now owns correctness/security maintenance for a protocol client it used to
get from a package) — the user's stated default preference is a maintained NuGet package, overridden here
only for that specific reason. `docs/tls-acme.md` — the existing architecture reference for this area — is
also stale: it still names `CertesAcmeClient` twice.

## What Changes

- Fix `docs/tls-acme.md`'s 2 stale `CertesAcmeClient` references.
- Add a new section documenting: the hand-roll's real RFC 8555 scope, a real completeness audit's findings
  (2 real gaps, several structurally-moot non-gaps explained), and explicit triggers for reconsidering a
  NuGet package later.
- Open a separate follow-up backlog item for the one real security-relevant gap found (certificate
  revocation) — not implemented here.

## Capabilities

Documentation-only — no product-facing behavior changes. `skip_specs: true` is set in this change's
`.openspec.yaml`.

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `docs/tls-acme.md` only (plus a new backlog item file for the revocation follow-up, not a code change).
