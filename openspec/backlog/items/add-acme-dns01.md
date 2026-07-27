---
id: add-acme-dns01
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: acme-companion DNS-01
provenance: this parity pass (matrix: DNS-01 ⛔)
---

## Why
HTTP-01 cannot issue **wildcard** certificates and needs port 80 reachable. nginx-proxy's acme-companion
supports DNS-01 with many providers. DockYarp only does HTTP-01, so wildcard/edge-restricted deployments can't
get ACME certs.

## nginx-proxy behavior
- acme-companion performs DNS-01 challenges via provider plugins (Cloudflare, Route53, etc.), enabling
  wildcard issuance without inbound port 80.

## DockYarp today
ACME HTTP-01 only (`src/DockYarp.Tls/CertesAcmeClient.cs`, `Http01ChallengeMiddleware.cs`); no DNS provider
integration (matrix ⛔).

## Proposed change (sketch)
Add a DNS-01 challenge path using Certes' DNS challenge support, with a pluggable DNS-provider abstraction
(start with one provider, e.g. Cloudflare, behind an interface). Config selects challenge type per host.
Enables wildcard `LETSENCRYPT_HOST`.

## Acceptance criteria (→ scenarios)
- **WHEN** a host is configured for DNS-01 with a provider **THEN** DockYarp publishes the `_acme-challenge`
  TXT record, completes validation, and installs the cert.
- **WHEN** a wildcard host uses DNS-01 **THEN** a `*.example.com` cert is issued.
- **WHEN** DNS-01 provider credentials are missing **THEN** provisioning fails with a clear error and HTTP-01
  hosts are unaffected.

## Notes / risks / references
- Provider abstraction + credential handling is the bulk of the work; scope to one provider first.
