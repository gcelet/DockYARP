## 1. Early reject (AG-RP)
- [x] 1.1 In `RequestBodySizeMiddleware`, short-circuit with 413 when the request's `Content-Length` exceeds the
      route limit (do not call `next`); otherwise set `MaxRequestBodySize` as the chunked backstop and continue

## 2. Tests (AG-RP)
- [x] 2.1 Unit (`RequestBodySizeMiddlewareTests`): a declared-oversized request returns 413 and `next` is not
      called (not proxied)
- [x] 2.2 Unit: a within-limit request calls `next` and gets the limit applied as the backstop. (The chunked /
      undeclared-length rejection is enforced by the Kestrel backstop and exercised by the e2e `limits.local`
      scenario.)
