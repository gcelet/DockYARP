## 1. Access log (AG-AA)

- [x] 1.1 Add `AccessLogOptions` (`Enabled`, default `true`)
- [x] 1.2 Add source-generated `AccessLog` (method/scheme/host/path/status/elapsed) and `AccessLogMiddleware`

## 2. Wiring (AG-AA)

- [x] 2.1 Add `AddDockYarpObservability(configuration)` (metrics + access-log options/middleware); use it in `Program`
- [x] 2.2 Add `UseMiddleware<AccessLogMiddleware>()` as the first pipeline middleware

## 3. Tests & docs

- [x] 3.1 Middleware tests: enabled → one entry emitted and next called; disabled → no entry, next called
- [x] 3.2 Document access logging (and the JSON option) in `docs/admin-api.md`
- [x] 3.3 Build + full test suite green via the Nuke CLI
