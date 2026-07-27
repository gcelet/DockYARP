## 1. Mitigation (AG-RP)
- [x] 1.1 In `ForwardedHeadersTransform`, remove the `Proxy` header from the proxied request

## 2. Tests & docs (AG-RP)
- [x] 2.1 Integration test (`tests/DockYarp.IntegrationTests`): a client-supplied `Proxy` header does not reach
      the backend
- [x] 2.2 Note the httpoxy mitigation in `docs/yarp-integration.md` (Forwarded headers)
