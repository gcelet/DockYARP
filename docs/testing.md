# Testing strategy & coverage

DockYarp follows a **test pyramid**. Each layer proves what the layer below cannot, so the slow/heavy end-to-end
suite stays small and every scenario earns its place.

| Layer | Where | Proves | Needs |
|-------|-------|--------|-------|
| **Unit** | `tests/DockYarp.*.Tests` (NUnit) | Pure logic: label/config parsing, resolvers, policy math, the parser + middleware internals. Most coverage lives here. | nothing |
| **Integration** | `tests/DockYarp.IntegrationTests` (`Microsoft.AspNetCore.Mvc.Testing`) | The **ASP.NET pipeline** in-process: admin API, headers, auth, redirects, access control, response compression, request limits. | no Docker |
| **End-to-end** | `tests/DockYarp.E2E.Tests` (Aspire AppHost + Docker) | Only what needs the **real running stack**: Docker discovery, live TLS/ALPN/ACME negotiation, protocol negotiation, edge-listener wiring, restart persistence. | Docker |

## When a behavior deserves an e2e (the rule)
Add an e2e **only** for a behavior that unit **and** integration tests cannot prove — i.e. it depends on the real
Docker discovery, a real TLS handshake (version/cipher/ALPN/mTLS), real ACME issuance, protocol negotiation, or the
actual Kestrel edge wiring. Pipeline behaviors (headers, auth, redirects, compression, access control) are proven in
**integration** and do **not** get an e2e. Keep the suite lean.

## E2E coverage map
Each backend scenario is a labeled container in `tests/DockYarp.E2E.AppHost/BackendCatalog.cs`; each test asserts a
runtime behavior through the real proxy.

| Test class · method | Proves (runtime) |
|---------------------|------------------|
| **DiscoveryTests** · `Whoami_IsDiscoveredAndProxied` | A labeled container is discovered and proxied. |
| · `MultiHost_BothHostsRoute` | One container under two `VIRTUAL_HOST`s routes on both. |
| · `ForwardedHeaders_ArePropagated` | `X-Forwarded-*` reach the backend. |
| · `UnknownHost_RoutesToDefaultBackend` | Unknown host → the default backend. |
| · `UnhealthyBackend_IsExcluded` | A Docker-unhealthy container is excluded from routing. |
| **RoutingTests** · `PathRewrite_StripsPrefix` | `VIRTUAL_PATH` + `VIRTUAL_DEST=/` strips the prefix before forwarding. |
| · `MultiPort_RoutesPerPath` | `VIRTUAL_HOST_MULTIPORTS` routes per path to different container ports. |
| · `Affinity_StickyClientRoutesToSameBackend` | `DOCKYARP_AFFINITY=ip-hash` sticks one client to one replica — proves the custom `ISessionAffinityPolicy` is wired into the live DI/middleware pipeline with a real client IP, which unit/integration cannot. |
| **EnvVarConfigTests** · `EnvOnlyBackend_IsDiscoveredAndProxied` | Config via environment variables (no labels). |
| · `EnvVar_OverridesSameNamedLabel` | Env value wins over a same-named label. |
| **TlsTests** · `AcmeCertificate_IsProvisionedForHost` | ACME (step-ca) provisions a cert for `LETSENCRYPT_HOST`. |
| · `AcmeWildcardCertificate_IsProvisionedViaDns01` | `DOCKYARP_ACME_CHALLENGE=dns-01` provisions a real wildcard certificate via RFC 2136 against a throwaway BIND9 authority — proves the hand-rolled DNS UPDATE/TSIG code, the DNS-01 challenge flow, and the wildcard parent-domain SNI fallback together, end to end. |
| · `UnknownHost_UsesSelfSignedFallback` | Unknown SNI host → the self-signed fallback certificate. |
| · `HttpRequest_RedirectsToHttps` | HTTP→HTTPS redirect on the edge. |
| · `Hsts_HeaderIsPresentOverHttps` | Per-host `HSTS` header served over HTTPS. |
| · `MutualTls_RejectsWithoutClientCertificate` / `MutualTls_AcceptsValidClientCertificate` | mTLS: a valid client cert is required and enforced at the handshake; the verified identity is passed through (`X-SSL-Client-Verify: SUCCESS` + subject). |
| · `MutualTlsOptional_NoCertificateSucceedsAsNone` / `_UntrustedCertificateSucceedsAsFailed` / `_ValidCertificateSucceedsAsSuccess` | mTLS `optional`: the handshake never fails on the client cert's trust outcome (a real TLS handshake genuinely not dropping an untrusted cert — unprovable below e2e); `X-SSL-Client-Verify` reflects `NONE`/`FAILED`/`SUCCESS`. |
| · `MutualTlsRequired_RevokedCertificateIsRejected` | A CRL-revoked client certificate fails the TLS handshake on a `required` host, even though it chains to the configured CA. |
| **SslPolicyNegotiationTests** · `ModernHost_RefusesTls12_AcceptsTls13` | Per-host `SSL_POLICY=Mozilla-Modern` floors the host at TLS 1.3 (a TLS 1.2 handshake is refused). |
| · `GlobalPostureHost_AcceptsTls12` | A host with no override keeps the global TLS 1.2 floor. |
| **Http2ToggleTests** · `DefaultHost_NegotiatesHttp2` | A default host negotiates HTTP/2 via ALPN. |
| · `DisabledHost_NegotiatesHttp11` | `DOCKYARP_HTTP2=false` restricts a host to HTTP/1.1 via ALPN. |
| **GrpcPassthroughTests** · `UnaryAndServerStreamingProxyThroughDockYarp` | `VIRTUAL_PROTO=grpc` → HTTP/2 gRPC (unary + server-streaming) proxied with trailers. |
| **SecurityTuningTests** · `BasicAuth_RejectsWithoutCredentials` / `_AcceptsWithCredentials` | Per-route Basic Auth through the real proxy. |
| · `MaxBodySize_RejectsOversizedBody` | `DOCKYARP_MAX_BODY_SIZE` rejects an oversized body. |
| · `ProxyTimeout_CancelsSlowResponse` | `DOCKYARP_PROXY_TIMEOUT` cancels a slow backend response. |
| **RestartPersistenceTests** · `ProvisionedCertificate_IsReusedAfterRestart` | A provisioned certificate survives a proxy restart (persistent store + Data Protection). |
| **AdminApiTests** · `Routes_ReflectDiscoveredContainers` / `Health_ReportsDiscoveryConnected` / `Routes_RejectMissingApiKey` | The admin API reflects live discovery and enforces the API key. |
| **ProxyProtocolTests** · `ProxyProtocolV1_…` / `ProxyProtocolV2_…` | A PROXY v1/v2 header on the edge recovers the real client IP into `X-Real-IP` / `X-Forwarded-For` (dedicated `Server:EnableProxyProtocol` instance). |
| **HostNetworkModeTests** · `HostNetworkBackend_IsReachedThroughDockYarp` | `Docker:HostAddress` reaches a real `--network host` backend created outside DCP (`NonDcpHarness`). **Requires Docker Desktop's "Enable host networking" beta setting on Windows/Mac** — without it, `--network host` doesn't route to the real host and this test fails locally with 502s (not a DockYarp bug); native Linux (this project's CI runner) needs no such setting. |
| **MultiNetworkTests** · `UnreachableNetworkBackend_FallsThroughToDefault` | `Docker:ProxyNetworks` auto-detection excludes a backend on a network the proxy doesn't share (`NonDcpHarness`); the request falls through to the default backend. |

## Deliberately **not** covered by e2e (and why)
- **Pipeline behaviors** (access control `NETWORK_ACCESS`, `SERVER_TOKENS`, response compression, forwarded-SSL
  headers, redirect status codes): proven in **integration** (`Mvc.Testing`) — no real stack needed.
- **Remote Docker daemon over TLS**: construction/verification unit-tested; a live `tcp://` TLS daemon in the test
  environment is not worth the setup.
- **Event debounce (reconcile coalescing)**: policy + loop unit-tested; a live timing assertion would be flaky.
- **HTTP/3 (QUIC)**: the feature itself is incomplete (needs MsQuic) → [`finish-http3`](../openspec/backlog/items/finish-http3.md).

## Keeping this in sync
When you add or remove an e2e test (or a `BackendCatalog` scenario), **update the coverage map above** in the same
change. When an e2e closes one of the "not covered" gaps, move its row into the map and drop it from the list.
