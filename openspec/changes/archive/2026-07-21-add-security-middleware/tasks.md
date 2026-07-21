## 1. Route auth metadata (AG-RP)

- [x] 1.1 Add `BasicAuthCredentials` (username, password, optional realm) to `DockYarp.Core/Models`
- [x] 1.2 Add optional `Auth` property to `RouteRule`
- [x] 1.3 Unit test: a route exposes its credentials; a route without them exposes none

## 2. Security project setup (AG-SEC)

- [x] 2.1 Add `Microsoft.AspNetCore.App` FrameworkReference to `DockYarp.Security`
- [x] 2.2 Implement `RouteLookup` (cached `RouteMatcher` rebuilt on store version change)

## 3. Middlewares (AG-SEC)

- [x] 3.1 Implement `SecurityHeadersMiddleware` (HSTS on HTTPS + baseline headers, configurable via `SecurityHeadersOptions`)
- [x] 3.2 Implement `HttpsRedirectionMiddleware` (redirect to HTTPS when the matched route enforces it)
- [x] 3.3 Implement `BasicAuthMiddleware` (401 + `WWW-Authenticate` when credentials missing/invalid; fixed-time compare)
- [x] 3.4 Add `AddDockYarpSecurity` (DI) and `UseDockYarpSecurity` (pipeline order) extensions

## 4. Host wiring (AG-SEC)

- [x] 4.1 Wire `AddDockYarpSecurity` + `UseDockYarpSecurity` before `MapReverseProxy` in `Program`

## 5. Tests (AG-SEC)

- [x] 5.1 `SecurityHeadersMiddleware`: baseline header present; HSTS only on HTTPS
- [x] 5.2 `HttpsRedirectionMiddleware`: redirect for enforced host; no redirect otherwise
- [x] 5.3 `BasicAuthMiddleware`: 401 without credentials, 200/next with valid, no challenge when unprotected

## 6. Documentation (AG-SEC)

- [x] 6.1 Document the security middleware (enforcement, auth, headers, ordering) in `docs/`
