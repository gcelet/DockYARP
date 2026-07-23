## 1. Shared resolution (AG-SEC)

- [x] 1.1 Add `RouteLookup.TryGetRoute(HttpContext, out RouteRule)` caching the result (incl. "no match") in `HttpContext.Items`
- [x] 1.2 Point the four security middlewares at `TryGetRoute`
- [x] 1.3 Point `RequestBodySizeMiddleware` (App) at `TryGetRoute`

## 2. Tests & build

- [x] 2.1 `RouteLookup` test: after resolving, a store change does not alter the route observed for the same request
- [x] 2.2 Build + full test suite green via the Nuke CLI
