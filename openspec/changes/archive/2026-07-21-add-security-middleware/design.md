## Context

Security runs as ASP.NET middleware in front of YARP (`MapReverseProxy`). Per-request decisions
(enforce HTTPS? require auth?) depend on the host/path, which comes from the `proxy-routing` snapshot. The
middleware must find the matching route cheaply per request. `DockYarp.Security` already depends on
`Core`; it will use the ASP.NET shared framework for middleware types.

## Goals / Non-Goals

**Goals:**
- Per-host HTTP→HTTPS redirect driven by the route's `EnforceHttps` flag.
- Route Basic Auth from credentials carried in the routing model.
- Configurable security headers (HSTS + baseline).
- Pipeline ordering that runs security before the proxy.

**Non-Goals:**
- No parsing of `DOCKYARP_AUTH_*` labels here (deferred to a `docker-discovery` update).
- No certificate-store integration; "TLS available" is represented by the model's `EnforceHttps` flag
  until `tls-acme` lands.
- No auth schemes beyond Basic.

## Decisions

- **Auth metadata on the model** via a new optional `RouteRule.Auth` (`BasicAuthCredentials`: username,
  password, optional realm). Added to `proxy-routing` as an additive requirement (ADDED, not MODIFIED —
  existing route behavior is unchanged). Kept array-free so record value equality (and store no-op
  detection) still holds.
- **Route lookup via a cached `RouteMatcher`.** A singleton `RouteLookup` rebuilds a `RouteMatcher` only
  when `store.Current.Version` changes (guarded by a lock), so per-request matching stays allocation-light.
  Rationale: reuse the existing matcher instead of a second index.
- **Three `IMiddleware` components** registered as singletons and ordered in an app extension
  `UseDockYarpSecurity`: headers (outermost, sets response headers) → HTTPS redirect → Basic Auth →
  (proxy). Redirect before auth avoids challenging a request that will be redirected.
- **`Microsoft.AspNetCore.App` FrameworkReference** on `DockYarp.Security` rather than individual
  packages — the idiomatic way for an ASP.NET library.
- **Redirect status** `308 Permanent` (preserves method) to the `https://` URL with the same host/path.

## Risks / Trade-offs

- `RouteLookup` rebuild under concurrency: a short lock on the rare version change; reads are otherwise
  lock-free via the matcher. [Risk: two rebuilds racing] → idempotent, worst case a duplicated build.
- Basic Auth compares credentials; use a fixed-time comparison to avoid timing leaks.

## Migration Plan

Additive: new `RouteRule.Auth` (optional), new middleware, host wiring. No data migration.

## Open Questions

- Whether HSTS should be limited to enforced hosts only — start with all HTTPS responses, revisit with
  `tls-acme`.
