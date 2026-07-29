## 1. Option + writer (AG-RP)
- [x] 1.1 `RoutingOptions`: add `DefaultResponseLocation` (redirect target template; null = status-only)
- [x] 1.2 `DefaultResponseWriter`: status-only when no location; otherwise redirect (status + substituted
      `Location`) with `$scheme`/`$host`/`$request_uri` and `$$` escape
- [x] 1.3 `Program.cs`: route `MapFallback` through `DefaultResponseWriter`

## 2. Docs (AG-DEP)
- [x] 2.1 `docs/deployment.md`: add `DefaultResponseLocation` to the `Routing` config row

## 3. Tests (AG-RP)
- [x] 3.1 `DefaultResponseWriter`: status-only unchanged; redirect with `$host`/`$request_uri` substitution;
      `$$` escape

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
