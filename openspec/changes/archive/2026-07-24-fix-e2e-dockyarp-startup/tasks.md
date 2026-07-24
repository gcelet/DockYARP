## 1. Harness fixes (AG-DEP)

- [x] 1.1 AppHost: remove `dockyarp.WaitFor(stepca)` (keep `WaitFor(dockerproxy)`); step-ca stays unwaited
- [x] 1.2 AppHost: remove the `--network-alias` runtime args from the DockYarp container (DCP rejects them)
- [x] 1.3 Remove the now-unused `NetworkAliasArgs` helper and `BackendCatalog.TlsHosts`

## 2. Build & validation

- [x] 2.1 `dotnet build` the AppHost + `./build.ps1 Test` green
- [x] 2.2 `openspec validate fix-e2e-dockyarp-startup --strict`
- [x] 2.3 Runtime: `./build.ps1 E2E` — DockYarp starts; 19/21 pass (all HTTP + fallback/mTLS TLS). The 2
      failures (`AcmeCertificate_IsProvisionedForHost`, `HttpRequest_RedirectsToHttps`) are the deferred
      HTTP-01 provisioning (served cert is the self-signed fallback) — see 3.1.

## 3. Follow-up (not this change)

- [ ] 3.1 HTTP-01 host resolution (step-ca → DockYarp) so the ACME-cert scenarios can pass
