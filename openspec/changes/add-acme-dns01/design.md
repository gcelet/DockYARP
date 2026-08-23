## Context

See `proposal.md` for motivation. Today `CertesAcmeClient.RequestCertificateAsync` always performs the
HTTP-01 challenge (`authorization.Http()`) via `IHttp01ChallengeStore`. `CertificateProvisioningService`
derives the set of hosts needing a certificate from `TlsDomains.Desired` and saves whatever
`RequestCertificateAsync` returns under the literal host string. `SniCertificateSelector.Select` already
has a wildcard fallback: if no exact-host certificate is found, it looks up the certificate stored under
the **parent domain** (leftmost label stripped) — this exists today for an operator-supplied wildcard
`CERT_NAME` certificate, not yet fed by ACME.

DockYarp's e2e suite already runs ACME HTTP-01 against a **local** `step-ca` instance
(`tests/DockYarp.E2E.AppHost/Program.cs`), not the real Let's Encrypt — this is the precedent this change
follows for DNS-01.

## Goals / Non-Goals

**Goals:**
- A DNS-01 challenge path in `CertesAcmeClient`, selected per host.
- A DNS-provider abstraction, with RFC 2136 (Dynamic DNS Update) as the first, real, e2e-tested
  implementation — chosen specifically because it needs no third-party account (see proposal.md) and is
  directly testable against a throwaway BIND9 container the same way HTTP-01 is tested against step-ca.
- Wildcard certificate issuance (`*.example.com`) for `dns-01` hosts, stored under the parent domain to
  reuse `SniCertificateSelector`'s existing lookup.

**Non-Goals:**
- A commercial DNS provider (Cloudflare, Route53, ...) — the abstraction is designed to accept one later,
  but none ships in this change (no account available to build or test against for real; see the
  proposal's provider discussion).
- DNS propagation *beyond* what step-ca/a real CA actually requires — DockYarp does not attempt to verify
  propagation against arbitrary public resolvers; it publishes the record and lets the CA's own DNS-01
  validation (a direct lookup against its configured resolver) be the source of truth, exactly like
  HTTP-01 lets step-ca's own HTTP fetch be the source of truth today.

## Decisions

- **DNS-01 challenge flow** (`CertesAcmeClient`): mirrors the existing HTTP-01 flow structurally.
  `authorization.Dns()` replaces `authorization.Http()`; the TXT record value is
  `acme.AccountKey.DnsTxt(challenge.Token)` (a Certes extension method — confirmed via `dotnet-inspect`,
  no new ACME-protocol code needed). The provider publishes `_acme-challenge.<host>` (or
  `_acme-challenge.<parent>` for a wildcard host — the ACME spec always challenges the base domain's
  `_acme-challenge` name, even for a wildcard identifier), waits briefly, calls `challenge.Validate()`,
  then removes the record in a `finally` block — same shape as `IHttp01ChallengeStore.Remove` today.
- **`IDnsChallengeProvider` abstraction**:
  ```csharp
  public interface IDnsChallengeProvider
  {
      Task PublishTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken);
      Task RemoveTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken);
  }
  ```
  `fqdn` is always `_acme-challenge.<zone-relative-or-absolute-name>` — the provider does not need to know
  about wildcards or the original requested host, only the literal record name to write.
- **RFC 2136 provider** (`Rfc2136DnsChallengeProvider`): **hand-rolled**, not via a third-party library.
  The obvious candidate, `ARSoft.Tools.Net` (2M+ downloads, has `DnsUpdateMessage`/`TSigRecord` types),
  was rejected on inspection: it targets `net6.0` only (no release since 2024-05-31) and depends on
  `BouncyCastle.Cryptography` — which directly conflicts (`CS0433`) with `Portable.BouncyCastle`, already
  pinned for CRL parsing in `add-mtls-optional-crl` (see `Directory.Packages.props`'s comment on that
  exact conflict class). No other maintained NuGet package offers RFC 2136 UPDATE + TSIG support. A DNS
  UPDATE message (RFC 2136 §2, built on the RFC 1035 message format) and a TSIG record (RFC 8945) are both
  small, well-specified binary structures — TSIG signing is one `HMACSHA256`/`HMACSHA1`/`HMACSHA384`/
  `HMACSHA512` call (BCL, no dependency) over a canonical byte sequence; the UPDATE message itself is a
  header + zone/prerequisite/update/additional sections, each a straightforward length-prefixed write.
  Scoped, bounded, and avoids reopening the BouncyCastle conflict — consistent with this project's existing
  preference (see the CRL work) for controlling exactly which crypto dependency enters the graph.
- **Config shape** (`TlsOptions`, flat fields matching the existing convention —
  `ClientCaCertificatePath`/`ClientCrlPath` are the precedent for a global, operator-level path/credential):
  `DnsUpdateServer` (host:port), `DnsUpdateZone`, `DnsUpdateTsigKeyName`, `DnsUpdateTsigKeySecret`,
  `DnsUpdateTsigAlgorithm` (default `hmac-sha256`). Global, not per-host — DNS infrastructure is an
  operator concern, matching `ClientCaCertificatePath`'s existing global scope.
- **Per-host opt-in**: new `HostTlsMetadata.ChallengeType` (`Http01` default, `Dns01`), mapped from a new
  `DOCKYARP_ACME_CHALLENGE` label (`http-01`/`dns-01`, case-insensitive, unrecognized value falls back to
  `http-01` with a warning — matching the project's established degrade-gracefully convention for invalid
  per-container config, e.g. `ContainerMapper.cs`'s precedent).
- **Wildcard storage**: `TlsDomains.Desired` and `CertificateProvisioningService` are unchanged in shape —
  a wildcard `CertificateHost` (`*.example.com`) flows through as-is for the ACME order (the CA requires
  the literal `*.example.com` identifier). `CertificateProvisioningService.ReconcileAsync` strips a leading
  `*.` before calling `certificates.Save(...)`, so the stored key is `example.com` — exactly what
  `SniCertificateSelector.Select`'s existing `ParentDomain` fallback already looks up. No change needed to
  `SniCertificateSelector` itself.
- **E2E test topology**: add a `bind9` container (ISC's official image, RFC 2136's reference
  implementation) with a minimal zone file and `allow-update { key <tsig-key>; };`, on the same Docker
  network as `stepca`. Point `stepca`'s DNS resolver at the `bind9` container (per the confirmed
  documentation: step-ca performs a plain DNS lookup against its configured resolver, with **no dependency
  on public DNS propagation** — private/internal DNS is an explicitly supported deployment shape). A new
  e2e test provisions a `*.dns01.example` (or similar) host via `DOCKYARP_ACME_CHALLENGE=dns-01`,
  confirms the cert is issued and served for `sub.dns01.example` (proving both the DNS-01 flow and the
  wildcard SNI fallback together).

## Risks / Trade-offs

- [Risk] Hand-rolling DNS UPDATE + TSIG is real protocol code, not a library call — budget real unit-test
  coverage (message-format round-trip, TSIG signature verification against a known-good vector) before
  trusting it against a live BIND9 server. → Mitigated by testing the wire format directly against RFC
  2136/8945's own worked examples before ever involving a real DNS server.
- [Risk] BIND9's container startup/zone-loading timing could be flaky in CI, mirroring step-ca's own
  documented startup-ordering care (`Program.cs`'s comments about not gating DockYarp on step-ca's health
  check) — budget for the same kind of iteration `fix-e2e-ci-runner-timeout` needed historically.
- [Trade-off] RFC 2136 requires the operator to run their own authoritative DNS server (or point a zone's
  NS records at one) — it is not a turnkey solution for someone using a commercial DNS host with no RFC
  2136 support (most consumer registrars). This is accepted scope: proposal.md documents the abstraction is
  designed to add a commercial provider later without disruption.

- **A wildcard order requests exactly one identifier** (`["*.example.com"]`), not also the bare parent
  domain. `TlsDomains.Desired` already maps one route to one desired host 1:1 — bundling a second identifier
  into the same order would mean `DesiredCertificate` carries a *set* of identifiers instead of one host,
  a materially bigger model change for a marginal convenience. If an operator also wants `example.com`
  bare, they declare a second route/host for it (which can independently use `http-01` or `dns-01`) —
  consistent with DockYarp's existing one-route-one-host declarative model, not a new concept.
