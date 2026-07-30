# Design — add-default-cert-trust-toggle

## Context
`SniCertificateSelector` returns the exact host cert, then the wildcard parent cert, then a self-signed
fallback from `DefaultCertificateProvider`. HTTP→HTTPS redirection (`HttpsRedirectionMiddleware`) already
consults `ICertificateAvailability` and does not redirect a host with no available certificate. There is no way
to supply an operator default certificate, to refuse hosts lacking a real certificate, or to force the redirect
before a certificate exists.

## Decisions

### 1. Operator default certificate via `default.crt`/`default.key` (nginx convention)
`DefaultCertificateProvider` looks for `default.crt` + `default.key` in `TlsOptions.CertificateDirectory`. When
both exist, it loads them (PEM → PKCS12 round-trip so the key is usable in Kestrel SNI across platforms) and
presents that certificate as the fallback; otherwise it keeps generating the self-signed one. No new path
option — the filenames match nginx. The provider gains `TlsOptions` + `IFileSystem` (already registered).

### 2. Reuse the existing PEM loader
The PEM→PKCS12 idiom already lives in `FileCertificateStore.TryLoadPem`. Extract it to an internal
`PemCertificateLoader.TryLoad(fileSystem, certPath, keyPath, out cert)` and call it from both the store and the
provider (DRY, one tested path). `FileCertificateStore.Load` reserves the `default` basename so `default.crt`
is owned by the fallback provider rather than registered as a host named `default`.

### 3. Trust + HTTP-on-missing are enforcement policy, decided in the middleware
Both toggles are consumed where the behavior lives — `HttpsRedirectionMiddleware`, which already has
`ICertificateAvailability`. They are added to `SecurityHeadersOptions` (the options that middleware layer
already binds) and the middleware inlines the two decisions (no separate flag-argument helper):
- **Trust** (`TrustDefaultCert`, default `true`): an HTTPS request to a host with no real certificate is
  refused with 500 when `false` (mirrors nginx `TRUST_DEFAULT_CERT=false`). The handshake still completes via
  the default certificate; the refusal is at the HTTP layer, so it is fully middleware-testable.
- **HTTP on missing cert** (`EnableHttpOnMissingCert`, default `true`): the redirect fires when the method
  redirects **and** (`certificate available` **or** `!EnableHttpOnMissingCert`). Default keeps today's behavior
  (no redirect until a certificate exists); `false` forces the redirect regardless.

### 4. No new runtime/e2e follow-up
Every seam is unit-testable: the provider (operator cert vs self-signed via a mock file system) and the
middleware (500 / redirect branches). Live SNI presentation is already exercised by the existing e2e TLS suite,
so no new e2e item is spawned.

## Risks
- A malformed operator `default.crt`/`.key` fails fast at startup (consistent with `FileCertificateStore`,
  which also does not swallow a bad PEM). This is intended: an explicitly supplied default certificate must be
  valid.
- `TrustDefaultCert=false` returns 500 for hosts lacking a real certificate — the intended refusal — so it must
  be enabled deliberately.
