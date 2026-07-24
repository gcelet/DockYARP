## 1. HTTP-01 sidecar (AG-DEP / AG-AT)

- [x] 1.1 AppHost: add an `alpine/socat` container with `WithContainerNetworkAlias` for `tls.local`,
      `hsts.local`, `mtls.local`
- [x] 1.2 AppHost: `WithArgs("TCP-LISTEN:80,fork,reuseaddr", "TCP:dockyarp:8080")` + `WaitFor(proxy)`
- [x] 1.3 AppHost: `Tls__CheckInterval=00:00:05` so provisioning retries after discovery (defeats the startup race)
- [x] 1.4 `AcmeCertificate_IsProvisionedForHost` polls until the served cert is the ACME one (not the fallback)
- [x] 1.5 Reduce the TLS poll budget to 60s for faster iteration
- [x] 1.6 Trust step-ca's full chain: harness writes a root+intermediate bundle; `SSL_CERT_FILE` points at it
      (root alone gives `PartialChain`)

## 2. Build & validation

- [x] 2.1 `dotnet build` the AppHost + `./build.ps1 Test` green
- [x] 2.2 `openspec validate add-e2e-acme-http01 --strict`
- [x] 2.3 Runtime: the harness drives in-cluster ACME correctly — verified live that step-ca is trusted and the
      HTTP-01 challenge is validated through the socat sidecar. Full green is **blocked by a product bug** the
      e2e exposed: `CertesAcmeClient` cannot build the cert chain for a private CA whose root the ACME server
      does not send (`Certes ... Can not find issuer ... Root CA`). Tracked as follow-up `fix-acme-private-ca-chain`.
- [ ] 2.4 (follow-up, separate changes) product fix `fix-acme-private-ca-chain`; redesign `Priority_HigherWins`
      (same-host aggregation → round-robin, not priority); harden the Nuke `Test` gate flake (empty `AdminApi.Tests`)
