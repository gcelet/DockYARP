## 1. Model (AG-RP)

- [x] 1.1 Add `HttpsMethod` enum (`Redirect`, `NoRedirect`, `NoHttp`, `NoHttps`) to `DockYarp.Core.Models`
- [x] 1.2 Replace `HostTlsMetadata.EnforceHttps` (bool) with `HttpsMethod Method` (default `Redirect`)

## 2. Discovery (AG-DD)

- [x] 2.1 Add `HttpsMethod` label constant; parse `HTTPS_METHOD` into `ContainerLabelConfig.HttpsMethod` (default `Redirect`); add `HasUnsupportedHttpsMethod`
- [x] 2.2 `ContainerMapper` sets `HostTlsMetadata.Method` and warns on an unrecognized value

## 3. Security & wiring (AG-SEC / AG-AT)

- [x] 3.1 Add `ICertificateAvailability` in Security; `HttpsRedirectionMiddleware` redirects when the method is redirecting AND a certificate is available
- [x] 3.2 Add `CertificateAvailabilityAdapter` (over `ICertificateStore`, exact + wildcard parent) in App and register it
- [x] 3.3 Replace `AdminApiModels.TlsView.EnforceHttps` with `HttpsMethod` (string); update `AdminMapper`

## 4. Tests & docs

- [x] 4.1 Parser/mapper tests: `HTTPS_METHOD` parsed to the method; unrecognized → `redirect` + warning
- [x] 4.2 Middleware tests: redirect when redirecting + cert available; no redirect when cert unavailable; no redirect for `noredirect`
- [x] 4.3 Document `HTTPS_METHOD` in `docs/labels-reference.md` and the cert-availability gate in `docs/security-middleware.md`
- [x] 4.4 Build + full test suite green via the Nuke CLI
