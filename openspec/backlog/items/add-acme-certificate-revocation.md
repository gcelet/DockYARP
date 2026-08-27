---
id: add-acme-certificate-revocation
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: n/a (internal finding — DockYarp doesn't expose an nginx-proxy-equivalent revocation mechanism
  either; this is about ACME-side capability, not parity)
provenance: 2026-08-27 add-acme-client-maintenance-policy's real RFC 8555 completeness audit of AcmeClient —
  the one gap found with a direct security consequence
---

## Why

`AcmeClient` (the hand-rolled ACME v2 client that replaced Certes, see `docs/tls-acme.md`'s "Client
maintenance & security" section) does not implement certificate revocation (RFC 8555 §7.6). If a
DockYarp-provisioned certificate's private key were ever compromised, there is currently no automated,
ACME-based way to revoke it — an operator would need to act directly against the CA out of band. This is the
one real gap the maintenance-policy audit found with a direct security consequence.

## nginx-proxy behavior

N/A — nginx-proxy itself has no built-in ACME revocation mechanism either (it relies on the same
acme-companion sidecar model, which itself wraps a client library); this is purely about closing a gap in
DockYarp's own hand-rolled client, not a parity item.

## DockYarp today

`AcmeClient`/`AcmeHttpClient` (`src/DockYarp.Tls/AcmeClient.cs`, `src/DockYarp.Tls/Acme/`) implement account
creation, order/challenge/finalize, and chain download — no revocation endpoint call, and nothing in
`ICertificateStore`/`CertificateProvisioningService` exposes a way to trigger one even if the client
supported it (no "revoke this cert" operation exists anywhere in DockYarp today, ACME or otherwise).

## Proposed change (sketch)

Not yet — this needs real design work, not a pre-committed approach:
1. Add the RFC 8555 §7.6 `revokeCert` wire call to `AcmeHttpClient` (JWS-signed POST with the certificate's
   DER bytes, optionally a revocation reason code per §7.6's own enum).
2. Decide how revocation is actually *triggered* operationally — this is the real design question, not the
   wire call itself: an Admin API endpoint? A CLI/dashboard action? Automatic revocation when an operator
   removes a host's TLS config? Each has different blast-radius and auth implications, needs its own
   consideration rather than assuming the answer.
3. Decide what happens to `ICertificateStore` state after a successful revocation (delete the persisted PEM
   pair so `CertificateProvisioningService` re-provisions fresh on next reconcile? leave it and rely on the
   CA-side revocation alone?).

## Acceptance criteria (→ scenarios)

TBD — depends on the trigger-mechanism design decision above. At minimum: an operator has *some* real,
documented way to revoke a DockYarp-provisioned certificate via ACME without needing out-of-band CA access.

## Notes / risks / references

- Low urgency in practice (no known incident driving this), but a real, named security gap — worth having a
  real design pass rather than leaving indefinitely undocumented.
- Refs: `docs/tls-acme.md`'s "Client maintenance & security" section (where this gap was first documented),
  `openspec/changes/archive/2026-08-25-investigate-certes-aot-alternative/` (the original hand-roll).
