## Context

`AccessLogMiddleware` logs every request. DockYarp's own endpoints — `/metrics` (frequent scrapes) and
`/api/*` — dominate the log with non-proxy noise.

## Goals / Non-Goals

**Goals:** skip access-log entries for configurable infrastructure path prefixes; keep everything else.

**Non-Goals:** per-status or sampling controls (deferred if needed).

## Decisions

- **`AccessLogOptions.ExcludedPathPrefixes`** (default `["/metrics", "/api"]`). The middleware skips logging
  when `request.Path.StartsWithSegments(prefix, OrdinalIgnoreCase)` matches any prefix — segment-aware, so
  `/api` matches `/api/routes` but not `/apiary`.
- The exclusion is checked alongside the existing `Enabled` short-circuit, before timing.

## Risks / Trade-offs

- Excluding `/api` also hides admin-API access from the log; that is the intent (infra endpoints), and it is
  configurable.

## Migration Plan

Additive option with a sensible default; the middleware gains a prefix check. No behavior change for proxied
paths.

## Open Questions

- None.
