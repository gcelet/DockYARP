## 1. Tls project setup (AG-AT)

- [x] 1.1 Add `Certes` to CPM and reference it in `DockYarp.Tls`; add the `Microsoft.AspNetCore.App` FrameworkReference
- [x] 1.2 Add `TlsOptions` (cert directory, contact email, ACME directory URI, accept-ToS, renewal margin, check interval)

## 2. Certificate store & fallback (AG-AT)

- [x] 2.1 Implement `ICertificateStore` + `FileCertificateStore` (load PFX dir at startup, Save writes + updates map, List)
- [x] 2.2 Implement the self-signed fallback certificate factory (generated at startup)

## 3. SNI & HTTP-01 (AG-AT)

- [x] 3.1 Implement the SNI certificate selector (host cert or fallback)
- [x] 3.2 Implement `KestrelTlsConfigurator : IConfigureOptions<KestrelServerOptions>` wiring the selector
- [x] 3.3 Implement `IHttp01ChallengeStore` + `Http01ChallengeMiddleware` (`/.well-known/acme-challenge/{token}`)

## 4. ACME client & provisioning (AG-AT)

- [x] 4.1 Define `IAcmeClient` (`RequestCertificateAsync(host, email, ct)`)
- [x] 4.2 Implement `CertesAcmeClient` (HTTP-01 via the challenge store) — integration-only
- [x] 4.3 Implement domain derivation from `HostTlsMetadata`
- [x] 4.4 Implement `CertificateProvisioningService : BackgroundService` (acquire missing, renew near-expiry) on start/change/timer

## 5. Host wiring (AG-AT)

- [x] 5.1 Add `AddDockYarpTls(options)` (store, fallback, selector, challenge, ACME client, provisioning, Kestrel configurator)
- [x] 5.2 Wire the ACME challenge middleware before the security pipeline in `Program`

## 6. Tests (AG-AT)

- [x] 6.1 `FileCertificateStore`: save/find/list; load from directory
- [x] 6.2 Fallback certificate is generated and non-null
- [x] 6.3 SNI selector: host cert when present, fallback otherwise
- [x] 6.4 `Http01ChallengeStore` + middleware: serves key authorization for a known token, 404 otherwise
- [x] 6.5 Domain derivation from TLS metadata
- [x] 6.6 `CertificateProvisioningService` with a fake `IAcmeClient`: acquires a missing cert; renews a near-expiry cert

## 7. Documentation (AG-AT)

- [x] 7.1 Document the TLS/ACME design (store, SNI, fallback, challenge, provisioning, staging default) in `docs/`
