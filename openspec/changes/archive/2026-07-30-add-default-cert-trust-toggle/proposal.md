## Why
nginx-proxy gives operators explicit control over what happens when a vhost has no real certificate: use an
operator-supplied `default.crt`, refuse it (`TRUST_DEFAULT_CERT=false` → error), or keep the vhost reachable
over HTTP (`ENABLE_HTTP_ON_MISSING_CERT`). DockYarp only has a self-signed fallback with no operator policy,
so these deployment choices cannot be expressed.

## What Changes
- **Operator default certificate**: when `default.crt`/`default.key` exist in the certificate directory, the SNI
  fallback presents them instead of the generated self-signed certificate.
- **Trust toggle** (`Security:TrustDefaultCert`, default `true`): when `false`, an HTTPS request to a host with
  no real certificate is refused (500) rather than served via the default certificate.
- **HTTP on missing cert** (`Security:EnableHttpOnMissingCert`, default `true`): formalizes the existing
  redirect gating — a host without a certificate stays reachable over HTTP; setting it `false` forces the
  HTTPS redirect even before a certificate exists.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: the default fallback certificate may be operator-supplied.
- `security`: HTTPS enforcement honors the trust and HTTP-on-missing-cert policies.

## Impact
- **Code**: `DockYarp.Tls` — `DefaultCertificateProvider` prefers `default.crt`/`default.key`; a shared
  `PemCertificateLoader` (extracted from `FileCertificateStore`); the store reserves the `default` basename.
  `DockYarp.Security` — `SecurityHeadersOptions.TrustDefaultCert`/`EnableHttpOnMissingCert`;
  `HttpsRedirectionMiddleware` refuses untrusted-default HTTPS and gates the redirect on the toggle.
- **Tests**: `DefaultCertificateProvider` (prefers operator cert, falls back to self-signed);
  `HttpsRedirectionMiddleware` (500 when untrusted + no real cert; redirect forced when HTTP-on-missing is off;
  defaults unchanged).
- **Owning agent**: AG-AT. Resolves `add-default-cert-trust-toggle`.
