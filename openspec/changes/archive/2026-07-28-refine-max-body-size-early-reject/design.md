## Context
`RequestBodySizeMiddleware.InvokeAsync` (`src/DockYarp.App/ReverseProxy/RequestBodySizeMiddleware.cs`) resolves
the route, reads `route.MaxRequestBodySize`, and — when `IHttpMaxRequestBodySizeFeature` is not read-only —
sets `feature.MaxRequestBodySize = maxBytes`, then calls `next`. Enforcement is lazy (during the body read), so
YARP starts forwarding before Kestrel throws `BadHttpRequestException`, producing a backend connection and a
warn-level stack trace (`Yarp.ReverseProxy.Forwarder.HttpForwarder[48]`).

## Goals / Non-Goals
- **Goal**: reject a *declared*-oversized body with 413 before proxying.
- **Non-Goal**: change the limit semantics; drop the chunked-body backstop; touch within-limit requests.

## Decisions
- In the middleware, when the route has a limit and `context.Request.ContentLength is { } declared && declared >
  maxBytes`, set `413` and short-circuit (do not call `next`). Otherwise set `MaxRequestBodySize` (backstop) and
  continue.
- `413` = `StatusCodes.Status413PayloadTooLarge`.

## Risks / Trade-offs
- A spoofed small `Content-Length` with a larger actual body is still caught by the Kestrel backstop (the
  streamed path), so the guarantee holds; only the common declared-length case is optimized.

## Migration Plan
- None: this strictly tightens an error path; within-limit requests are unchanged.

## Open Questions
- Whether to also quiet the forwarder `warn` for the streamed path — left as a follow-up.
