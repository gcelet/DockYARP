## Why

The Aspire TLS end-to-end suite (running against a local **step-ca** ACME server) exposed a real product bug:
certificate provisioning **fails against a private/custom CA**. `CertesAcmeClient` builds the certificate with
`chain.ToPfx(key).Build(...)`, and Certes' `PfxBuilder` defaults to `FullChain = true`, which resolves the
issued certificate all the way up to a **root** it recognises. An ACME server (per convention) returns the
leaf + intermediate but **not the root**, and Certes only bundles the well-known public roots (Let's Encrypt),
so for a private CA it throws:

```
Certes.AcmeException: Can not find issuer '…Root CA' for certificate '…Intermediate CA'
   at Certes.Pkcs.CertificateStore.GetIssuers
```

So provisioning works with Let's Encrypt but never completes with step-ca (or any private ACME CA), even
though the whole ACME exchange — account, order, HTTP-01 validation — succeeds. DockYarp does not actually
need the root in the certificate it serves (clients trust the CA out of band).

## What Changes

- `CertesAcmeClient` builds the PFX with the full chain when it can (public CAs — unchanged behaviour), and
  **falls back to the leaf certificate** when Certes cannot complete the chain to a root (private/custom CAs).
  Concretely: attempt `PfxBuilder.Build` with `FullChain = true`; on the chain-resolution failure, retry with
  `FullChain = false`. Let's Encrypt provisioning is unaffected; private-CA provisioning now succeeds.

## Capabilities

### Modified Capabilities
- `tls-acme`: ACME certificate acquisition also succeeds against a private/custom CA whose root is not part of
  the issued chain.

## Impact

- **Code**: `src/DockYarp.Tls/CertesAcmeClient.cs` (certificate-building fallback). No API or config change.
  Plus a test-harness tweak (`tests/DockYarp.E2E.Tests/TlsHarness.cs`): the TLS client disables connection
  reuse so the e2e poll re-handshakes and actually observes the provisioned certificate.
- **Behaviour**: Let's Encrypt unchanged (full chain served); private CAs now provision (leaf served — the
  known trade-off is that the intermediate is not bundled in that fallback path).
- **Unblocks**: the two ACME end-to-end scenarios (`AcmeCertificate_IsProvisionedForHost`,
  `HttpRequest_RedirectsToHttps`) archived under `add-e2e-acme-http01`.
- **Owning agent**: AG-AT.
