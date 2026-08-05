## 1. Add the headers (AG-RP)
- [x] 1.1 `ForwardedHeadersTransform`: set `X-Forwarded-Ssl` (`on`/`off`) from the first hop of
      `X-Forwarded-Proto` (fallback `Request.IsHttps`), removed-then-set
- [x] 1.2 `ForwardedHeadersTransform`: set `X-Original-URI` = `PathBase` + path + query, removed-then-set

## 2. Tests (AG-RP)
- [x] 2.1 `ForwardedHeadersIntegrationTests`: the echo backend also reports `X-Forwarded-Ssl` + `X-Original-URI`;
      assert `X-Forwarded-Ssl: off` over HTTP and `X-Original-URI` carries the original path + query

## 3. Docs (AG-DOC)
- [x] 3.1 Site `features.md`: document `X-Forwarded-Ssl` + `X-Original-URI` in the forwarded-headers behavior

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green (unit/integration, no Docker)
