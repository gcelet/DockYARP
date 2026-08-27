## Why

`AcmeClient`/`AcmeHttpClient` implement account creation, order/challenge/finalize, and chain download, but not
RFC 8555 §7.6 certificate revocation. If a DockYarp-provisioned certificate's private key were ever
compromised, there is no automated, ACME-based way to revoke it today — an operator would need to act
directly against the CA out of band. This is the one gap from `add-acme-client-maintenance-policy`'s
completeness audit with a direct security consequence.

## What Changes

- `AcmeHttpClient` gains the RFC 8555 §7.6 `revokeCert` wire call (JWS-signed POST of the certificate's DER
  bytes, no revocation reason sent — the field is optional and no operator-facing reason selection exists
  yet).
- `IAcmeClient`/`AcmeClient` gain a `RevokeCertificateAsync` operation, signing the revocation request with
  the same persisted account key (`AcmeAccountKeyStore`, from `add-acme-account-persistence`) that would be
  used to (re-)issue for that host's (contact email, ACME directory endpoint) pair.
- `ICertificateStore`/`FileCertificateStore` gain a `Remove` operation: on successful revocation, the stored
  PEM pair (and any legacy PFX) for that host is deleted so the existing reconcile loop re-provisions a fresh
  certificate (and fresh key) on its next pass — deliberately, since the whole point of revoking for a
  compromised key is to stop using that key, not just to tell the CA about it.
- The admin dashboard gains a "Revoke" action on `/dashboard`, mirroring the existing "Convert to PEM"/
  "Re-encrypt key" actions' pattern (POST + antiforgery token), gated by its **own** new opt-in
  `AdminApi:AllowCertificateRevocation` — deliberately not reusing `AllowCertificateConversion`, since
  revocation is a materially higher-consequence action (it takes a host offline until re-provisioning
  completes) than a format-only rewrite.

## Capabilities

### Modified Capabilities
- `tls-acme`: adds ACME certificate revocation as a new capability of the ACME client and certificate store.
- `admin-api`: adds a second mutating dashboard action (revocation) alongside the existing conversion/
  re-encryption pair; the "Read-only admin dashboard" requirement's own text ("exactly one narrow ... mutating
  action") needs updating too, not just a new requirement.

## Impact

- `src/DockYarp.Tls/Acme/AcmeHttpClient.cs`, `AcmeDirectory.cs` (new optional `revokeCert` URL field — RFC
  8555 marks it OPTIONAL in the directory object).
- `src/DockYarp.Tls/AcmeClient.cs`, `IAcmeClient.cs`, `tests/DockYarp.Tls.Tests/FakeAcmeClient.cs` (new
  interface member, needs a fake implementation too).
- `src/DockYarp.Tls/ICertificateStore.cs`, `FileCertificateStore.cs` (new `Remove` operation).
- `src/DockYarp.AdminApi/AdminApiOptions.cs` (new `AllowCertificateRevocation` option).
- `src/DockYarp.Dashboard/DashboardEndpointMapping.cs`, `DashboardViewModel.cs`, the `Dashboard` Razor slice
  (new POST route + button, gated on the new option).
- Docs: `docs/tls-acme.md` (closes the revocation gap), `docs-site/content/en/docs/configuration.md` (new
  option row; the existing "these are the **only** mutating actions" sentence needs updating too — a real
  case for `AGENTS.md`'s doc-audit habit, not just the literal new row).
