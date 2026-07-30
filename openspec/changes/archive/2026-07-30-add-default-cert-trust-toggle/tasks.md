## 1. TLS default certificate (AG-AT)
- [x] 1.1 Extract `PemCertificateLoader.TryLoad(fileSystem, certPath, keyPath, out cert)` and refactor
      `FileCertificateStore` to use it
- [x] 1.2 `FileCertificateStore.Load`: reserve the `default` basename (do not register `default.crt` as a host)
- [x] 1.3 `DefaultCertificateProvider`: prefer `default.crt`/`default.key` from the certificate directory
      (via `TlsOptions` + `IFileSystem`), else the self-signed certificate

## 2. Security policy (AG-SEC)
- [x] 2.1 `SecurityHeadersOptions`: add `TrustDefaultCert` (default true) and `EnableHttpOnMissingCert`
      (default true)
- [x] 2.2 `HttpsRedirectionMiddleware`: refuse HTTPS with 500 for a host with no real certificate when
      `TrustDefaultCert=false`; gate the redirect on `EnableHttpOnMissingCert`

## 3. Tests (AG-AT/AG-SEC)
- [x] 3.1 `DefaultCertificateProvider`: prefers an operator `default.crt`/`.key`; falls back to self-signed
      when absent
- [x] 3.2 `HttpsRedirectionMiddleware`: 500 when untrusted and no real cert; redirect forced when HTTP-on-missing
      is off; existing defaults unchanged

## 4. Verify (AG-AT)
- [x] 4.1 Nuke `Test` gate green
