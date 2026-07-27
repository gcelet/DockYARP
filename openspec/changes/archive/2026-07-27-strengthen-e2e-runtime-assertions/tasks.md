## 1. Strengthen existing e2e scenarios (AG-DEP)
- [x] 1.1 In `DiscoveryTests.Whoami_IsDiscoveredAndProxied`, assert the proxied response carries no `Server`
      header
- [x] 1.2 In `TlsTests.HttpRequest_RedirectsToHttps`, assert the redirect status is exactly 308

## 2. Spec (AG-DEP)
- [x] 2.1 Add the "End-to-end runtime security assertions" requirement to the deployment spec
