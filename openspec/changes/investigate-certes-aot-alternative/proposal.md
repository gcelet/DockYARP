## Why

A real `-p:PublishAot=true` spike measured 170 remaining trim/AOT warnings after all 3 prior AOT-prep items
landed; 136 of them (by far the largest bucket) trace to `Newtonsoft.Json`, pulled in transitively by
`Certes 3.0.4` (`DockYarp.Tls`'s ACME client). A further 5 are downstream BCL consequences of Newtonsoft's own
`dynamic`/expression-tree usage — together ~141 of 170, the single largest remaining blocker to a
warning-free Native AOT publish.

This investigation checked every real avenue before committing to an approach, per this backlog item's own
"verify before concluding blocked" instruction:
- **Original Certes**: confirmed dead (`fszlin/certes`, last push 2024-02-07, no newer release at 3.0.4).
- **3 direct forks checked** (NuGet + GitHub, not just descriptions): `PPioli.Certes.AOT` (drops
  Newtonsoft, but 1 star/0 forks/16 months stale — too unvalidated for a TLS-critical dependency);
  `CertesSlim` (drops Newtonsoft, actively published, but no public source repository exists anywhere —
  unauditable, and its namespace (`CertesSlim.*`) is not drop-in compatible); `Webprofusion.Certify.
  ACME.Anvil` (by far the best-maintained — pushed days before this check — but confirmed via the real NuGet
  catalog entry to still depend on `Newtonsoft.Json >= 13.0.3`, even in its unreleased GitHub source — does
  not solve the problem at all).
- **2 further user-supplied leads, both fully verified**: `jjrdk/opencertserver` is an ACME *server*, not a
  client; the two projects it cites (`FluffySpoon.AspNet.EncryptWeMust`, `PKISharp/ACME-Server`) are either
  built directly on top of Certes (confirmed via source) or also a server. `natemcmaster/LettuceEncrypt`
  (1685 stars, archived) confirmed via its own `.csproj` to depend on the exact same `Certes 3.0.4` DockYarp
  already uses — a convenience wrapper, not an alternative.
- **No candidate simultaneously satisfies "removes Newtonsoft.Json" AND "trustworthy enough for a
  TLS-critical dependency."**

The real Certes usage surface, read directly from `src/DockYarp.Tls/CertesAcmeClient.cs` (the ONLY file in
the codebase referencing `Certes.*`), is narrow: account creation, single-host order creation, HTTP-01 or
DNS-01 challenge retrieval + trigger, authorization-status polling (already hand-rolled, not a Certes
feature), CSR generation from a fresh ES256 key, and issued-chain retrieval. No account persistence, no
revocation, no multi-domain orders, no RSA. This is a bounded RFC 8555 subset, comparable in scope to
`add-acme-dns01`'s own successful hand-roll of RFC 2136 DNS UPDATE + TSIG when no suitable package existed
for that item either.

## What Changes

- Replace `Certes` with a hand-rolled ACME v2 client inside `DockYarp.Tls`, built entirely on
  `System.Security.Cryptography` (`ECDsa`, `CertificateRequest`) + `System.Text.Json` — zero new
  dependencies, zero Newtonsoft.Json anywhere in the dependency graph.
- `CertesAcmeClient` (renamed) keeps the exact same `IAcmeClient` contract — every caller
  (`CertificateProvisioningService`, DI wiring in `TlsServiceCollectionExtensions`) is unaffected.
- Same behavior preserved end-to-end: HTTP-01 and DNS-01 challenges, chain-building (leaf + every returned
  issuer, no self-signed-root PKIX path requirement), the existing 30×2s authorization-status poll.

## Capabilities

Behavior-preserving internal implementation swap — the `tls-acme` capability's existing requirements
(HTTP-01/DNS-01 provisioning, chain handling) are unchanged; nothing in `openspec/specs/tls-acme/spec.md`
references Certes by name (confirmed: describes behavior, not implementation). `skip_specs: true` is set in
this change's `.openspec.yaml`.

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `src/DockYarp.Tls/CertesAcmeClient.cs` — full rewrite of the ACME protocol calls, same public shape.
- New files under `src/DockYarp.Tls/Acme/` (or similar) for the JWS signing, JWK thumbprint, and ACME
  resource/directory model — see `design.md` for the exact breakdown.
- `Directory.Packages.props` / `DockYarp.Tls.csproj` — remove the `Certes` package reference.
- `tests/DockYarp.Tls.Tests/` — new unit tests for the parts testable without a live CA (JWS structure, JWK
  thumbprint, CSR building).
- `tests/DockYarp.E2E.Tests/TlsTests.cs` — existing HTTP-01/DNS-01 e2e tests exercise the new client
  end-to-end against the real step-ca test authority; no new e2e infrastructure needed.
