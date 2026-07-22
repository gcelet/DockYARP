## 1. Bind options from configuration (AG-DEP)

- [x] 1.1 Bind `Tls` section → `TlsOptions` (ContactEmail, CertificateDirectory, AcmeDirectoryUri, AcceptTermsOfService, renewal margins) in `Program.cs`
- [x] 1.2 Bind `Security` section → `SecurityHeadersOptions`
- [x] 1.3 Bind `Docker` section → `DockerDiscoveryOptions` (keep the `Docker:Enabled` gate separate)
- [x] 1.4 Bind `AdminApi` section → `AdminApiOptions`
- [x] 1.5 Preserve defaults when keys are absent (staging ACME, default headers, closed admin API)

## 2. Discoverability (AG-DEP)

- [x] 2.1 Add the section skeletons to `appsettings.json` (aligned with defaults)
- [x] 2.2 Document the configuration keys in `docs/deployment.md`

## 3. Tests & verification (AG-DEP)

- [x] 3.1 Integration test: setting `Tls:AcmeDirectoryUri`/`Tls:AcceptTermsOfService`, `Security:*`, `AdminApi:ApiKey` is reflected in the resolved options
- [x] 3.2 Build + full test suite green via the Nuke CLI (`./build.ps1 Test`)
