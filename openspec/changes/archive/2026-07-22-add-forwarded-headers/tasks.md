## 1. Forwarded headers transform (AG-RP)

- [x] 1.1 Implement `ForwardedHeadersTransform.Apply(context, trustDownstreamProxy)`: `UseDefaultForwarders = false`, `AddXForwarded(Append|Set)`, `AddOriginalHost(true)`
- [x] 1.2 Add a custom request transform setting `X-Real-IP` (RemoteIpAddress) and `X-Forwarded-Port` (LocalPort)

## 2. Host wiring (AG-RP)

- [x] 2.1 Read `Proxy:TrustDownstreamProxy` (default true) and chain `.AddTransforms(...)` on the reverse proxy in `Program.cs`

## 3. Tests & docs (AG-RP)

- [x] 3.1 Integration test: a backend echoes forwarded headers; assert `X-Forwarded-Proto`, forwarded `Host`, and `X-Forwarded-Host` reach it
- [x] 3.2 Document forwarded headers + `Proxy:TrustDownstreamProxy` in `docs/yarp-integration.md`
- [x] 3.3 Build + full test suite green via the Nuke CLI
