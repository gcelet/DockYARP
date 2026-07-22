## ADDED Requirements

### Requirement: HTTPS method label
The system SHALL read `HTTPS_METHOD` from a container's labels and set the route's HTTPS method
(`redirect` (default), `noredirect`, `nohttp`, `nohttps`) on its TLS metadata, defaulting to `redirect`
when absent and falling back to `redirect` (with a warning) for an unrecognized value. The method applies to
a host that carries TLS metadata (a certificate host).

#### Scenario: HTTPS_METHOD selects the redirect policy
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HTTPS_METHOD=noredirect`
- **THEN** the mapped route's TLS metadata carries the `noredirect` method

#### Scenario: Unrecognized value falls back to redirect
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HTTPS_METHOD=bogus`
- **THEN** the route's TLS metadata uses `redirect` and the invalid value is reported
