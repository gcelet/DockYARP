## Why

Five pipeline middlewares (security headers, HTTPS redirection, client-certificate, Basic Auth, request
body-size) each call `RouteLookup.TryMatch(host, path)` per request, resolving the same route up to five
times. The matcher is cached per store version, but the lookup still repeats on the hot path.

## What Changes

- Add `RouteLookup.TryGetRoute(HttpContext, out route)` which resolves the matched route **once per request**
  and caches the result (including "no match") in `HttpContext.Items`, then reuses it.
- The five middlewares call `TryGetRoute` instead of `TryMatch`, so a request resolves its route a single
  time. No observable routing behavior changes.

## Capabilities

### Modified Capabilities
- `security`: the request pipeline resolves the matched route at most once per request and shares it.

## Impact

- **Code**: `src/DockYarp.Security` (`RouteLookup`, the four security middlewares),
  `src/DockYarp.App/ReverseProxy` (`RequestBodySizeMiddleware`).
- **Owning agent**: AG-SEC / AG-RP.
