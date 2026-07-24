## Context

`CertesAcmeClient.RequestCertificateAsync` finishes the ACME order and then does
`chain.ToPfx(privateKey).Build(host, "")`. Certes' `PfxBuilder.FullChain` defaults to `true`, so `Build`
resolves the issued leaf up to a trusted root via `CertificateStore.GetIssuers`. Certes only ships the public
roots, and ACME servers do not return the root, so a private CA (step-ca) yields
`Can not find issuer '…Root CA'`. The e2e proved everything up to this point works (account, order, HTTP-01
validation); only the local PFX assembly fails.

## Goals / Non-Goals

**Goals:** provisioning succeeds against a private/custom ACME CA; Let's Encrypt behaviour is unchanged.

**Non-Goals:** bundling the intermediate in the private-CA fallback path (deferred — see trade-off); changing
the ACME exchange, options, or the certificate store.

## Decisions

- **Attempt full chain, fall back to leaf.** Build the `PfxBuilder` from the issued chain and call `Build`
  with the default `FullChain = true`. If Certes cannot complete the chain to a root it throws `AcmeException`;
  catch it, set `FullChain = false`, and rebuild. Public CAs take the first path (full chain served, unchanged);
  private CAs take the fallback (leaf served). The fallback is scoped to the `Build` call, so it does not mask
  unrelated ACME errors from earlier steps.
- **Why not always `FullChain = false`?** That would regress Let's Encrypt — the intermediate would no longer
  be served, breaking clients that rely on the server sending it. The try/fallback keeps the good public-CA
  behaviour.

## Risks / Trade-offs

- **Private-CA fallback serves the leaf only** (no intermediate bundled). Clients of a private CA must obtain
  the intermediate out of band, or the CA must be single-tier. Acceptable for the current goal; bundling the
  issued intermediate without the root is a possible later enhancement.
- Catching `AcmeException` around `Build` is narrow (only the chain-assembly step), so genuine ACME failures
  still surface from the earlier `NewAccount`/order/validation calls.

## Migration Plan

Single method change in `CertesAcmeClient`; no signature, option, or store change. Existing Let's Encrypt
deployments behave exactly as before.

## Open Questions

- Whether to also bundle the issued intermediate in the fallback path (serve leaf+intermediate for private
  CAs). Deferred; not required to unblock the e2e or typical single-tier private CAs.
