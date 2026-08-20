---
id: add-admin-dashboard-cert-conversion
capability: admin-api
agent: AG-AA
tier: B-runtime
priority: low
nginx-proxy: n/a (DockYarp value-add)
status: backlog
provenance: 2026-08-20 user feedback, same session as `change-cert-store-format-to-pem` and
  `add-admin-dashboard-cert-download` — refined after clarification: the real motivating case is an existing
  DockYarp-managed installation with a mix of certs (16 already `.crt`/`.key`, 1 still `.pfx` from before
  `change-cert-store-format-to-pem` shipped), wanted a one-click way to normalize the outlier without deleting
  it and waiting for re-provisioning.
---

## Why

An operator with an existing DockYarp installation can have a mix of on-disk certificate formats after
`change-cert-store-format-to-pem` ships: hosts renewed/provisioned since the change are `.crt`/`.key`, hosts
not yet renewed are still `.pfx` (`FileCertificateStore.Load()` already reads either, so nothing is broken —
this is purely a consistency/cleanup itch, not a bug). The operator already knows the workaround (delete the
`.pfx`, force re-provisioning) but wants a one-click dashboard action instead of waiting out a real ACME
round-trip for a certificate that's already valid and already loaded in memory.

## Current state

- Not started.
- `FileCertificateStore.Save(host, certificate)` already writes PEM unconditionally (shipped in
  `change-cert-store-format-to-pem`) — converting a currently-`.pfx`-backed host to PEM is mechanically just
  calling `Save()` again with the certificate `ICertificateStore.Find(host)` already has loaded in memory. No
  ACME round-trip, no new parsing/conversion logic needed for this direction.
- **Not currently exposed anywhere**: nothing today reports *which on-disk format* backs a given host's loaded
  certificate — `ICertificateInventory`/`CertificateInfo` only carry host + expiry. A "convert" action needs to
  know which rows are actually `.pfx`-backed to only offer the button where it's meaningful.
- **This would be the first mutating action ever added to the admin surface.** Every existing requirement is
  explicit about this: "Read-only admin endpoints" (`/api/*`), "Read-only admin dashboard" (`/dashboard`) — both
  requirement *titles* state read-only, not just their scenarios. This item would need to either modify that
  framing or introduce a narrowly-scoped, explicitly-called-out exception.
- Distinct from `add-admin-dashboard-cert-download` (a read-only export of already-managed material — no
  mutation, no format ambiguity) — do not assume they share a design just because both are cert-related and
  both surfaced from the same feedback session.

## Proposed change (sketch)

Not designed — needs propose-time decisions, but the shape is much narrower than originally scoped (an earlier
draft of this item conflated it with converting an *external* PFX a user has outside DockYarp; that need is
already fully covered by `FileCertificateStore.Load()` accepting `.pfx` directly, or by `openssl` locally — no
DockYarp feature required for that case, and none is proposed here):

- Expose, per stored certificate, whether it's currently `.pfx`-backed (new field or a parallel lookup) so the
  dashboard can show a "convert to PEM" action only where it applies.
- The action itself: re-`Save()` the already-loaded `LoadedCertificate` for that host (writes the PEM pair),
  then delete the stale `.pfx` file for that host (the passive `Save()` path never does this today — an
  explicit user-triggered conversion is a reasonable place to finally do it, unlike a background/automatic
  deletion which was deliberately not built into `change-cert-store-format-to-pem`).
- **Open security question for propose/design time, not resolved here**: does a mutating dashboard action need
  more than `Surface == ApiAndDashboard`? It doesn't extract any *new* secret material (unlike download — the
  private key involved was already reachable via volume access either way), but it does write to disk for the
  first time from an HTTP-triggered action. Consider whether it needs its own opt-in (mirroring
  `AllowCertificateDownload`'s pattern) or is acceptable under the dashboard's existing gate alone.
- Likely needs a `### Requirement: ...` addition to `admin-api` and a note on "Read-only admin dashboard" that
  this one specific, narrow action is the exception (not a general reopening of "read-only").

## Acceptance criteria (→ scenarios)

- **WHEN** an operator triggers the conversion action for a `.pfx`-backed host **THEN** that host's certificate
  is rewritten as `{host}.crt`/`{host}.key` and the stale `.pfx` is removed, with the certificate still served
  correctly afterward (no re-provisioning, no served-certificate change).
- **WHEN** a host's certificate is already PEM-backed **THEN** no conversion action is offered/available for it.

## Notes / risks / references

- Depends on `change-cert-store-format-to-pem` (shipped) for `Save()` already writing PEM, and pairs naturally
  with `add-admin-dashboard-cert-download` (shipped) as the second dashboard-adjacent certificate-management
  action, but is mechanically and risk-wise distinct — do not silently reuse that item's design decisions
  without re-checking they still apply (e.g. its opt-in gate was scoped to *secret exposure*, which doesn't
  apply the same way here).
