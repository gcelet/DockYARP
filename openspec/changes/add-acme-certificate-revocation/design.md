## Context

See `proposal.md` - Why. `AcmeHttpClient` (`src/DockYarp.Tls/Acme/`) implements account creation,
order/challenge/finalize, and chain download, but no `revokeCert` call. `ICertificateStore`/
`FileCertificateStore` (`src/DockYarp.Tls/`) have no removal operation — only `Save`, `ConvertToPem`,
`ReencryptPrivateKey`. `add-acme-account-persistence` (shipped) means every host now resolves to a
deterministic, persisted account key keyed by (contact email, ACME directory endpoint) — the same key a
revocation request for that host can reuse without any new "which account issued this" bookkeeping.

The admin dashboard (`src/DockYarp.Dashboard/DashboardEndpointMapping.cs`) already has exactly one precedent
for a mutating, opt-in, antiforgery-protected POST action: `ICertificateConverter`'s "Convert to PEM"/
"Re-encrypt key", gated by `AdminApiOptions.AllowCertificateConversion`. Its own remarks describe it as "the
first (and only) mutating action the admin surface exposes" — this change makes it the second, and that
precedent's shape (own interface, own opt-in flag, same antiforgery pattern) is the natural template.

## Goals / Non-Goals

**Goals:**
- A real, working ACME revocation call, reachable only through an explicit operator action.
- Certificate store state stays consistent with "this key is no longer trusted": revoking removes the local
  copy so a fresh certificate (and fresh key) replaces it automatically.

**Non-Goals:**
- Automatic/triggered revocation (e.g. on a host's TLS config being removed, or on some external compromise
  signal) — this change is operator-initiated only. An automatic trigger is a much larger design question
  (what counts as "compromised"? what if revocation itself fails mid-removal?) not motivated by any real
  scenario today.
- Exposing a revocation reason code (RFC 8555 §7.6's optional `reason` field, RFC 5280 `CRLReason` values) —
  no operator-facing reason-selection UI exists, and the field is optional; add it later if a real need
  surfaces (e.g. CA-side handling actually differs by reason for some provider).
- A JSON admin API (`/api/*`) endpoint for revocation — the dashboard's antiforgery-protected form is the
  established mutating-action surface; `/api/*` today is read-only (`AdminEndpoints.cs`'s own docstring). Adding
  a mutating JSON endpoint would need its own auth story beyond the shared `X-Api-Key`, out of scope here.

## Decisions

**Trigger mechanism**: a dashboard POST action (`/dashboard/certs/{host}/revoke`), mirroring
`PostConvertAsync`/`PostReencryptAsync` exactly — antiforgery validation, then the mutation, then redirect
back to `/dashboard`. Considered and rejected: an Admin API (`/api/*`) endpoint (that surface is read-only by
design today, and reusing the shared API key for a destructive action has a materially different risk profile
than the dashboard's own trust boundary); a CLI (no CLI surface exists in this project at all); automatic
triggering (a Non-Goal above).

**Its own opt-in, not `AllowCertificateConversion`**: `AllowCertificateConversion` was deliberately scoped to
actions that "only rewrite the on-disk format of an already-served certificate" (its own doc comment).
Revocation is categorically different — it takes the host offline (served via the self-signed fallback) until
the next reconcile pass re-provisions it, and it makes an irreversible call to the CA. A new
`AdminApiOptions.AllowCertificateRevocation` (default `false`) keeps that distinction explicit rather than
silently broadening what the existing flag means.

**Signing key**: the request is signed with the same persisted account key `AcmeAccountKeyStore` resolves for
the host's (contact email, ACME directory endpoint) pair — the same key that issued it (or would issue a
replacement). RFC 8555 §7.6 also allows signing with the certificate's own key pair (useful when the account
key itself is unavailable/compromised); not implemented here since DockYarp always has its persisted account
key available in the same process that has the certificate — there's no scenario yet where one is reachable
but not the other.

**Store removal, not just a wire call**: revocation without removing the local copy would leave DockYarp
still serving a certificate it just told the CA is untrusted, for up to `Tls:CheckInterval` (default 12h)
until the next reconcile pass happens to notice... except the reconcile loop's own `NeedsCertificate` check
(`CertificateProvisioningService.cs`) only re-provisions on **absence** or **near-expiry**, never on
revocation status (ACME has no "is this still valid" check DockYarp polls) — so without an explicit `Remove`,
a revoked certificate would keep being served indefinitely. Removing it immediately makes `NeedsCertificate`
return `true` on the very next pass, reusing that existing mechanism rather than adding a new one.

## Risks / Trade-offs

- **[Risk]** Revoking removes the host's certificate before a replacement exists — the host falls back to the
  self-signed default certificate until the next reconcile pass completes (bounded by `Tls:CheckInterval`, or
  immediate if a pass is already due). **Mitigation**: this is the intended behavior (a still-trusted
  certificate for a possibly-compromised key is worse than a brief self-signed gap); an operator revoking a
  live host should expect and plan for a short availability dip, which is inherent to what revocation means,
  not an implementation shortcoming.
- **[Risk]** A CA that omits the optional `revokeCert` URL from its directory would need a clear failure, not
  a null-reference. **Mitigation**: `AcmeDirectory.RevokeCert` is nullable; `AcmeClient` checks for its
  absence and fails with an actionable error before attempting the store removal (so a CA without revocation
  support never gets its certificate deleted for nothing).
- **[Risk]** The revoke action being on `/dashboard` (network-isolation-only trust boundary, same as every
  other dashboard action) means anyone reaching that host can revoke any certificate once the operator opts
  in. **Mitigation**: identical trust model to the existing conversion/re-encryption/download actions — opt-in
  and documented, not a new category of exposure this change introduces.

## Migration Plan

Purely additive: `AllowCertificateRevocation` defaults to `false`, so no existing deployment's behavior
changes until an operator explicitly opts in. No data migration. Rollback (reverting this change) is safe —
no persisted state depends on the revoke action having existed.
