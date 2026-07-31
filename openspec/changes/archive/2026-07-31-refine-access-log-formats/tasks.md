## 1. Options + fields (AG-AA)
- [x] 1.1 `AccessLogOptions`: add `Fields` (`string[]?`, default null → current behavior)
- [x] 1.2 New pure `AccessLogFields`: the field catalog, `Select(catalog, fields)` (ordered, canonical-cased,
      unknown skipped), and a `key=value` message `Format`

## 2. Middleware (AG-AA)
- [x] 2.1 `AccessLogMiddleware`: when `Fields` is set, build the catalog, select, and log a structured state;
      otherwise keep the existing `AccessLog.Request` fast path

## 3. Tests (AG-AA)
- [x] 3.1 `AccessLogFields.Select`: a custom order returns exactly those fields in order; unknown names are
      skipped (case-insensitive, canonical-cased); `Build` exposes the canonical catalog

## 4. Verify (AG-AA)
- [x] 4.1 Nuke `Test` gate green
