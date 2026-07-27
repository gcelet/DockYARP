---
id: add-cert-name-shared-cert
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: low
status: backlog
nginx-proxy: CERT_NAME
provenance: this parity pass (matrix: CERT_NAME ⛔)
---

## Why
nginx-proxy's `CERT_NAME` lets several vhosts share one SAN/wildcard certificate by name (`shared.crt`/
`shared.key`) instead of the per-host `<host>.crt` convention. DockYarp selects certs by host only, so a
single multi-SAN cert can't be pinned to a set of hosts.

## nginx-proxy behavior
- `CERT_NAME` (per proxy or per container) forces use of `/etc/nginx/certs/<CERT_NAME>.crt`+`.key`, taking
  precedence over the automatic per-host / wildcard-parent selection.

## DockYarp today
Cert selection is per host with wildcard-parent fallback (`src/DockYarp.Tls/SniCertificateSelector.cs`,
`FileCertificateStore.cs`); provided PEM/PFX are named `<host>`. No named/shared-cert override.

## Proposed change (sketch)
Add a per-host TLS metadata field (from a `CERT_NAME` label / config) that pins SNI selection to a named cert
in the certificate store, taking precedence over host-based lookup. Extend `HostTlsMetadata` + the SNI
selector.

## Acceptance criteria (→ scenarios)
- **WHEN** two hosts set `CERT_NAME=shared` and `shared.crt/.key` exists **THEN** both are served that cert via
  SNI.
- **WHEN** `CERT_NAME` is set but the named cert is missing **THEN** it falls back per current rules with a
  warning.

## Notes / risks / references
- Decide the trigger surface (label vs static config); nginx-proxy allows both.
