## 1. Model (AG-RP)

- [x] 1.1 Add `ClientCertificateRequirement` enum (`None`, `Optional`, `Required`) to Core.Models
- [x] 1.2 Add `RouteRule.ClientCertificate` (default `None`)

## 2. Discovery (AG-DD)

- [x] 2.1 Add `DOCKYARP_CLIENT_CERT` label; parse into `ContainerLabelConfig` (default `None`); add `HasUnsupportedClientCert`
- [x] 2.2 `ContainerMapper` sets `RouteRule.ClientCertificate` and warns on an unrecognized value

## 3. CA validation (AG-AT)

- [x] 3.1 Add `TlsOptions.ClientCaCertificatePath`; add `ClientCertificateValidator` (loads CA via `IFileSystem`, validates chain)
- [x] 3.2 `KestrelTlsConfigurator` requests + validates client certs when a CA is configured; register the validator

## 4. Enforcement (AG-SEC)

- [x] 4.1 Add `ClientCertificateMiddleware`: a `Required` route with no connection certificate → 403; register and add to the pipeline

## 5. Tests & docs

- [x] 5.1 Validator tests: CA-signed cert accepted; unrelated cert rejected; no CA → not required
- [x] 5.2 Middleware tests: required + no cert → 403; required + cert → pass; none → pass
- [x] 5.3 Parser/mapper tests: label parsed and carried; unrecognized → warning
- [x] 5.4 Document `DOCKYARP_CLIENT_CERT` and `ClientCaCertificatePath` in the docs
- [x] 5.5 Build + full test suite green via the Nuke CLI
