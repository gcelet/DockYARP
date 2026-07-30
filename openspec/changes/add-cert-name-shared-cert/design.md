# Design — add-cert-name-shared-cert

## Context
SNI selection (`SniCertificateSelector.Select(host)`) resolves a certificate by host: exact `store.Find(host)`,
then the wildcard-parent certificate, then the self-signed fallback. TLS metadata (`HostTlsMetadata`) is
attached to a route only when `LETSENCRYPT_HOST` is set; it drives ACME provisioning (`TlsDomains.Desired`) and
redirect/HSTS behavior. There is no way to pin a host to a named shared certificate.

## Decisions

### 1. `CERT_NAME` is per-host TLS metadata
Add `HostTlsMetadata.CertificateName` (from a `CERT_NAME` label). Because a shared-certificate host is HTTPS
even without ACME, `ContainerMapper` creates TLS metadata when **either** `LETSENCRYPT_HOST` **or** `CERT_NAME`
is set. For a `CERT_NAME`-only host the `CertificateHost` is the vhost itself (used for HTTPS method/HSTS), and
ACME does not provision it.

### 2. A shared host→name resolver
`CertificateNameResolver.Resolve(snapshot, host)` scans the routing snapshot for a route whose host pattern
matches `host` and whose TLS metadata carries a `CertificateName`. It is pure and reused by both the SNI
selector and the certificate-availability adapter, so the two never diverge. Host matching reuses the existing
`HostPattern` (exact and wildcard), so a `CERT_NAME` on a wildcard host applies to its subdomains.

### 3. SNI override with fallback
`SniCertificateSelector` gains the routing store and a logger. `Select(host)` first resolves a `CERT_NAME`; if
the named certificate exists in the store it is returned (pinned), overriding host-based lookup. If a
`CERT_NAME` is configured but the named certificate is absent, selection falls back to the existing per-host
rules and logs a warning **once per missing name** (SNI runs per handshake, so the warning is deduplicated to
avoid log spam).

### 4. Exclude shared-certificate hosts from ACME
`TlsDomains.Desired` skips routes carrying a `CertificateName`: the operator supplies the shared certificate, so
per-host ACME would provision an unused certificate (and risk rate limits). Grouping `LETSENCRYPT_HOST` vhosts
under one ACME SAN certificate via `CERT_NAME` (multi-domain orders) is out of scope.

### 5. Redirect gating recognizes the shared certificate
`CertificateAvailabilityAdapter.IsAvailable(host)` additionally returns true when the host resolves to a
`CERT_NAME` whose certificate is in the store, so a shared-certificate host redirects HTTP→HTTPS like any other
HTTPS host. It uses the same `CertificateNameResolver`.

## Risks
- A `CERT_NAME` typo (named certificate not mounted) leaves the host on the self-signed fallback; the
  deduplicated warning surfaces it without spamming the log.
- Setting both `LETSENCRYPT_HOST` and `CERT_NAME` yields shared-certificate behavior (no per-host ACME), which
  matches the manual-shared-certificate intent; the multi-SAN-ACME interpretation is explicitly not supported.
