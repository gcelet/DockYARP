## Why
nginx-proxy's `CERT_NAME` lets several vhosts share one SAN/wildcard certificate by name (`shared.crt`/
`shared.key`) instead of the per-host `<host>.crt` convention. DockYarp selects certificates by host only, so a
single multi-SAN certificate cannot be pinned to a set of hosts.

## What Changes
- A `CERT_NAME` label pins a host's TLS to a named certificate in the certificate store, taking precedence over
  the per-host / wildcard-parent lookup during SNI selection.
- A host carrying `CERT_NAME` is treated as an HTTPS host (redirect/HSTS metadata) even without
  `LETSENCRYPT_HOST`, and is **not** individually provisioned via ACME (it uses the operator-supplied shared
  certificate). Redirect gating recognizes the shared certificate as available for the host.
- When the named certificate is missing, selection falls back to the current per-host rules.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: SNI selection honors a `CERT_NAME` shared-certificate override.

## Impact
- **Code**: `DockYarp.Core` (`HostTlsMetadata.CertificateName`); `DockYarp.Docker` (`DockerLabels.CertName`,
  `ContainerLabelConfig.CertName`, `LabelParser`, `ContainerMapper` TLS metadata); `DockYarp.Tls`
  (`CertificateNameResolver`, `SniCertificateSelector` override, `TlsDomains` ACME exclusion); `DockYarp.App`
  (`CertificateAvailabilityAdapter` recognizes the shared certificate).
- **Tests**: `CertificateNameResolver` (host→name resolution), `SniCertificateSelector` (override served;
  missing name falls back), `TlsDomains` (a `CERT_NAME` host is excluded from ACME), `LabelParser`/
  `ContainerMapper` (`CERT_NAME` parsed and mapped).
- **Out of scope**: grouping several `LETSENCRYPT_HOST` vhosts under one ACME-provisioned SAN certificate via
  `CERT_NAME` (multi-domain ACME orders) — documented as a follow-up.
- **Owning agent**: AG-AT. Resolves `add-cert-name-shared-cert`.
