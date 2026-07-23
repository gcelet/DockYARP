## Context

DockYarp generates bodiless status responses (default 404/503, `nohttps` 404, client-cert 403, Basic Auth
401). nginx-proxy serves branded error pages. This adds configurable `{statusCode}.html` pages for the errors
DockYarp produces itself, without buffering proxied responses.

## Goals / Non-Goals

**Goals:** load `{statusCode}.html` from a directory and write it as the body of a not-yet-started error
response (status ≥ 400) that has no body.

**Non-Goals (deferred):** rewriting already-streamed backend error bodies (needs response buffering, which
would defeat streaming), per-host pages, and templating.

## Decisions

- **`ErrorPageProvider`** loads pages once via `IFileSystem`: files named `{code}.html` in
  `ErrorPages:Directory` become a `code → html` map; a missing directory yields no pages (fail-open).
- **`ErrorPageMiddleware`** wraps the pipeline and, after `next`, writes the page when
  `!Response.HasStarted`, status ≥ 400, a page exists for that status, and `ContentLength` is null/0. This
  covers DockYarp's own error responses (which set only a status); a response a backend already started is
  left untouched — no buffering, so streaming is unaffected.
- Placed just after access logging so it observes the final status and covers the fallback, redirect-refusal,
  and auth responses.

## Risks / Trade-offs

- Backend error bodies (already started) aren't customized — a deliberate trade to avoid buffering every
  proxied response. Documented; response-buffering is deferred.

## Migration Plan

Additive: options + provider + one middleware. No pages configured ⇒ the middleware is a pass-through and
behavior is unchanged.

## Open Questions

- Response buffering to also cover backend-produced error bodies — deferred; revisit if operators need it.
