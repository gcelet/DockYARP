# Design — add-doc-capability-reference (bounded: application-configuration reference)

## Scope
The proxy's own configuration sections only. The label/env surface is already documented
(`add-doc-feature-reference`); the site audit + runtime-feature narrative are deferred to
`add-doc-runtime-reference`.

## Source of truth (defaults verified from the options types)
- `Server` → `ServerEndpointOptions` (`HttpPort` 8080, `HttpsPort` 8443).
- `Docker` → `DockerDiscoveryOptions` + the `Docker:Enabled` gate (default false); `DockerEndpoint`,
  `PreferredNetwork`, `ProxyNetworks`, `HostAddress`, `ContainerFilters`, reconnect delays (1s / 30s).
- `Tls` → `TlsOptions` (`CertificateDirectory` `certs`, `AcmeDirectoryUri` LE **staging**, `AcceptTermsOfService`
  false, `RenewBeforeExpiry` 30d, `CheckInterval` 12h, `MinimumTlsVersion` Tls12, `SslPolicy`, `CipherSuites`,
  `HttpProtocols` Http1AndHttp2, `ClientCaCertificatePath`).
- `Security` → `SecurityHeadersOptions` (`EnableHsts` true, `HstsMaxAge` 365d, `TrustDefaultCert` true,
  `EnableHttpOnMissingCert` true, `FrameOptions` DENY, `ReferrerPolicy` no-referrer, `ServerHeader` suppressed,
  `InternalRanges` private+::1, `HtpasswdDirectory`, `HtpasswdReloadInterval` 30s).
- `Routing` → `RoutingOptions` (`DefaultHost`, `DefaultResponseStatusCode` 404, `DefaultResponseLocation`).
- `Proxy` → `TrustDownstreamProxy` (true).
- `AccessLog` → `AccessLogOptions` (`Enabled` true, `ExcludedPathPrefixes` /metrics,/api, `Fields`).
- `AdminApi` → `AdminApiOptions` (`ApiKey`, empty = closed).
- `Compression` → `Compression:Enabled` (true).
- `DataProtection` → `DataProtectionOptions` (`CertificatePath`, `CertificatePassword`).
- `Host` → `Host:ShutdownTimeoutSeconds` (30).

## Content
One subsection per section with a small `Key | Default | Purpose` table, and a closing note that any key can be
set via `appsettings.json` or a `Section__Key` environment variable. Cross-links to the container-config
reference and the parity matrix stay.
