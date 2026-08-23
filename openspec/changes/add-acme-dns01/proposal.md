## Why

ACME HTTP-01 (DockYarp's only challenge type today) cannot issue **wildcard** certificates and requires
inbound port 80 reachable per host. nginx-proxy's acme-companion supports DNS-01 via provider plugins,
closing both gaps. DockYarp needs a DNS-01 path to reach parity and unlock wildcard/edge-restricted
deployments.

## What Changes

- Add a DNS-01 challenge path to the ACME client (`CertesAcmeClient`), using Certes' `IAuthorizationContext.Dns()`
  challenge and `IKey.DnsTxt(token)` to compute the TXT record value — the same library already used for
  HTTP-01, no new ACME dependency.
- Add a pluggable DNS-provider abstraction (`IDnsChallengeProvider`: publish/remove a TXT record), with
  **RFC 2136 (Dynamic DNS Update)** as the first concrete provider — not a commercial API. RFC 2136 is the
  generic mechanism cert-manager/Traefik/Certbot/Posh-ACME all support for talking to any self-hosted
  authoritative DNS server (BIND, PowerDNS, CoreDNS, Technitium) via a TSIG key, over the standard DNS
  protocol (UDP/TCP port 53) — no third-party account of any kind. The abstraction stays open for a
  commercial provider (Cloudflare, Route53, ...) to be added later behind the same interface.
- Add a per-host opt-in: a new `DOCKYARP_ACME_CHALLENGE` label (`http-01` default, `dns-01` opt-in) plus the
  RFC 2136 server/zone/TSIG-key configuration (global, matching `ClientCaCertificatePath`'s existing
  global-config convention — DNS infrastructure is an operator-level concern, not a per-container one).
- **Wildcard host support**: a `dns-01` host may declare a wildcard `CertificateHost` (`*.example.com`).
  The ACME order requests `*.example.com` (DNS-01 is the only challenge type the ACME protocol permits for
  a wildcard identifier — enforced by the CA, not by DockYarp); the issued certificate is stored under the
  **parent domain** (`example.com`), matching `SniCertificateSelector.Select`'s existing wildcard fallback
  lookup (`ParentDomain`) — that lookup already exists (built for an operator-supplied wildcard cert) and
  needs no change; only provisioning needs to save under the stripped key.
- **Real, provider-agnostic DNS-01 e2e coverage**: the existing e2e topology already validates ACME HTTP-01
  against a **local** `step-ca` instance (not the real Let's Encrypt) — see `tests/DockYarp.E2E.AppHost/Program.cs`.
  step-ca's DNS-01 validation is a plain DNS lookup against whatever resolver it's configured with; it does
  not require real public DNS propagation. Adding a throwaway BIND9 container (RFC 2136's own reference
  implementation) to the same Docker network, with step-ca's resolver pointed at it, lets DockYarp's real
  RFC 2136 provider publish a TXT record that step-ca genuinely validates — end to end, no mocking, no
  third-party account, mirroring the HTTP-01 precedent exactly.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `tls-acme`: adds the DNS-01 challenge path, the `IDnsChallengeProvider` abstraction + RFC 2136 provider,
  the `DOCKYARP_ACME_CHALLENGE` label, and wildcard certificate issuance/storage.

## Impact

- `src/DockYarp.Tls/` — `CertesAcmeClient` (DNS-01 branch), new `IDnsChallengeProvider` +
  `Rfc2136DnsChallengeProvider`, `TlsOptions` (RFC 2136 server/zone/TSIG config), `TlsDomains`/
  `CertificateProvisioningService` (wildcard host → parent-domain storage key).
- `src/DockYarp.Core/Models/HostTlsMetadata.cs` — new `ChallengeType` (or similar) property.
- `src/DockYarp.Docker/` — label mapping for `DOCKYARP_ACME_CHALLENGE`.
- Tests: unit tests for the DNS-01 orchestration (fake `IDnsChallengeProvider`) and the RFC 2136 provider's
  DNS-update-message construction; a new e2e test (`AcmeWildcardCertificate_IsProvisionedViaDns01`) against
  a real BIND9 container + step-ca.
- `docs/tls-acme.md`, `docs/labels-reference.md`, `docs-site/content/en/docs/configuration.md` — document
  the new label, RFC 2136 config, and wildcard support.
