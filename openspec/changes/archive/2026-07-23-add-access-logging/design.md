## Context

DockYarp logs subsystems (discovery, TLS) but not the requests it serves. Access logging is a
high-throughput, cross-cutting concern, so it uses source-generated logging (like `DiscoveryLog`/`TlsLog`)
and sits first in the pipeline to observe every response.

## Goals / Non-Goals

**Goals:** a structured per-request access log (method, scheme, host, path, status, elapsed ms), toggleable
via configuration, covering all responses (proxied, redirected, unmatched).

**Non-Goals:** a custom log-line format (text/JSON rendering is the logging provider's job), request/response
body or byte-size accounting, and per-route log customization.

## Decisions

- **`AccessLogMiddleware` (App.Observability, `IMiddleware`)** times the request with
  `Stopwatch.GetTimestamp()`/`GetElapsedTime` and, in a `finally`, emits one entry via a source-generated
  `AccessLog.Request(...)`. Placed as the **first** middleware so redirects and 404s are logged too.
- **`AccessLogOptions.Enabled`** (default `true`) bound from the `AccessLog` section; when disabled the
  middleware is a pass-through.
- **Registration fold**: an `AddDockYarpObservability(configuration)` extension registers the metrics
  (moved from `Program`) plus the access-log options and middleware, keeping `Program` within the
  statement-count limit. The pipeline adds one `UseMiddleware<AccessLogMiddleware>()` call.
- **Structured, not formatted**: fields are named template values; JSON output is achieved by configuring
  the .NET console logger, documented rather than reimplemented.

## Risks / Trade-offs

- Logging every request adds overhead; source-generated logging keeps it allocation-free, and the toggle
  lets operators disable it. Elapsed time is wall-clock middleware time, not the backend's own latency.

## Migration Plan

Additive: new middleware + options + registration extension; metrics registration is relocated (behavior
unchanged). Access logging is on by default; set `AccessLog:Enabled=false` to opt out.

## Open Questions

- A dedicated combined-format access log and byte/size fields — deferred; revisit if operators need
  nginx-identical log lines.
