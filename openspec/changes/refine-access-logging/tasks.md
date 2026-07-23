## 1. Exclusion (AG-AA)

- [x] 1.1 Add `AccessLogOptions.ExcludedPathPrefixes` (default `/metrics`, `/api`)
- [x] 1.2 `AccessLogMiddleware` skips logging when the path starts with an excluded prefix (segment-aware)

## 2. Tests & build

- [x] 2.1 Middleware test: a request under an excluded prefix is not logged; a normal path still is
- [x] 2.2 Build + full test suite green via the Nuke CLI
