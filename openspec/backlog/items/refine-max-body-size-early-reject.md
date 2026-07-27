---
id: refine-max-body-size-early-reject
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: e2e diagnostics log review, 2026-07-27 (dockyarp resource log)
---

## Why
The per-route body-size limit (`DOCKYARP_MAX_BODY_SIZE`) is enforced by Kestrel's `MaxRequestBodySize`, which
only trips **while YARP is already forwarding the request body to the backend**. An oversized request therefore
(a) opens a backend connection for nothing, and (b) logs a ~60-line YARP `AggregateException` stack trace at
`warn` on every occurrence — before returning 413. The result is correct but wasteful and a log-spam vector
(an attacker sending large bodies floods the logs and touches the backend).

## Observed behavior (e2e log — exact signatures)
For a POST to `limits.local` exceeding the 1024-byte limit:
```
info: Yarp.ReverseProxy.Forwarder.HttpForwarder[9]   Proxying to http://172.18.0.4:8080/ HTTP/2 ...
warn: Yarp.ReverseProxy.Forwarder.HttpForwarder[48]  RequestBodyClient: The client reported an error when copying the request body.
      System.AggregateException: ... (Request body too large. The max request body size is 1024 bytes.) ...
        ---> Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException: Request body too large. The max request body size is 1024 bytes.
        at ... Yarp.ReverseProxy.Forwarder.StreamCopier.CopyAsync(...)   [~60 lines]
info: DockYarp.App.Observability.AccessLogMiddleware[1]  POST http://limits.local/ responded 413 in 34.4 ms
```
So the forward starts (`HttpForwarder[9]`), the body copy fails mid-stream (`HttpForwarder[48]`), then 413.

## DockYarp today
`src/DockYarp.App/ReverseProxy/RequestBodySizeMiddleware.cs` (whole file, `InvokeAsync`): it looks up the route,
reads `route.MaxRequestBodySize` (nullable `long` bytes, from the `DOCKYARP_MAX_BODY_SIZE` label), and — when the
`IHttpMaxRequestBodySizeFeature` is not read-only — sets `feature.MaxRequestBodySize = maxBytes`, then calls
`next(context)`. Enforcement is lazy (during the body read, mid-forward); there is **no up-front check**. The
middleware is registered in `src/DockYarp.App/Program.cs` (`app.UseMiddleware<RequestBodySizeMiddleware>()`)
after security and before `MapReverseProxy()`.

## Proposed change (sketch)
Add an up-front `Content-Length` check in `RequestBodySizeMiddleware.InvokeAsync`, short-circuiting **before**
proxying; keep setting `MaxRequestBodySize` as the backstop for chunked / undeclared-length bodies:
```csharp
if (routes.TryGetRoute(context, out RouteRule? route) && route.MaxRequestBodySize is { } maxBytes)
{
    if (context.Request.ContentLength is { } declared && declared > maxBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return Task.CompletedTask;                 // reject before any backend connection / forwarder log
    }

    if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
    {
        feature.MaxRequestBodySize = maxBytes;      // backstop for chunked / no Content-Length
    }
}

return next(context);
```
Optionally soften/suppress the `HttpForwarder[48]` warning for the streamed-body case (a client body-size
rejection is expected, not a server error).

## Acceptance criteria (→ scenarios)
- **WHEN** a request declares a `Content-Length` above the route's max body size
- **THEN** it is rejected with 413 before any backend connection is made, and no `HttpForwarder` warning/stack
  trace is logged
- **WHEN** an over-limit body is sent without a `Content-Length` (chunked transfer)
- **THEN** it is still rejected via the Kestrel `MaxRequestBodySize` backstop, returning 413
- **WHEN** a within-limit request is sent
- **THEN** it proxies normally

## Notes / risks / references
- Internal quality finding (log hygiene + efficiency), **not an nginx-proxy parity gap** — no `parity.md` row.
- Tests to extend: `tests/DockYarp.IntegrationTests/RequestBodySizeMiddlewareTests.cs` (add the early-`Content-Length`
  413 path + assert no forwarder attempt). The streamed case is already exercised by the e2e `limits.local`
  scenario.
- `413` = `StatusCodes.Status413PayloadTooLarge`. Confirm `RouteRule.MaxRequestBodySize` type/units (bytes).
