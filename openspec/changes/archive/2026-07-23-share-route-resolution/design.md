## Context

`RouteLookup.TryMatch(host, path)` is called by five middlewares per request. Each recomputes the same
lookup. The matcher itself is cached per store version, so the cost is a dictionary lookup ×5 — small, but
redundant and repeated on the hot path.

## Goals / Non-Goals

**Goals:** resolve a request's route once and share it; no change to which route is selected.

**Non-Goals:** changing matching semantics, or caching across requests (the cache is per request).

## Decisions

- **`RouteLookup.TryGetRoute(HttpContext, out RouteRule)`**: on first call it resolves via the existing
  `TryMatch` (reading the request host/path) and stores the result in `HttpContext.Items` under a private
  key — a sentinel marks "no match" so a null result is cached too. Subsequent calls return the cached value.
- The five middlewares call `TryGetRoute(context, …)` instead of `TryMatch(host, path, …)`. `TryMatch`
  remains for direct/unit use.
- Per-request caching also makes route observation **stable** within a request even if the store is updated
  mid-flight — a minor correctness bonus.

## Risks / Trade-offs

- Caching in `HttpContext.Items` costs one dictionary entry per request; negligible versus the repeated
  lookups it removes.

## Migration Plan

Internal refactor: new method + call-site swaps. No public routing behavior changes; existing middleware
tests exercise the new path unchanged.

## Open Questions

- None.
