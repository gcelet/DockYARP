## 1. Options (AG-SEC)
- [x] 1.1 Add a nullable `ServerHeader` property to `SecurityHeadersOptions` (default null = suppressed)
- [x] 1.2 It binds from the `Security` configuration section (bound in the App host like the other headers)

## 2. Suppress + emit (AG-SEC)
- [x] 2.1 Set `KestrelServerOptions.AddServerHeader = false` in the App host so the default `Server: Kestrel`
      header is not added
- [x] 2.2 In `SecurityHeadersMiddleware`, when `ServerHeader` is non-empty, set the `Server` response header to
      that value

## 3. Tests & docs (AG-SEC)
- [x] 3.1 Unit test (`tests/DockYarp.Security.Tests`): middleware emits `Server` when configured
- [x] 3.2 Unit test: middleware leaves `Server` absent when not configured (the built-in header is disabled at
      the host; real-Kestrel suppression is exercised by the e2e/manual path)
- [x] 3.3 Document the `Security:ServerHeader` option and the new default in `docs/security-middleware.md`
