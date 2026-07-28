## Why
The per-route body-size limit (`DOCKYARP_MAX_BODY_SIZE`) is enforced only by Kestrel's `MaxRequestBodySize`,
which trips **while YARP is already forwarding the body to the backend**. An oversized request therefore opens
a backend connection for nothing and logs a ~60-line YARP `AggregateException` stack trace at `warn`
(`HttpForwarder[48]`) before returning 413. Correct, but wasteful and a log-spam vector (surfaced by the e2e
diagnostics logs on `limits.local`).

## What Changes
- Reject an oversized request **up front** in `RequestBodySizeMiddleware` when its `Content-Length` exceeds the
  route limit: 413, no backend connection, no forwarder stack trace.
- Keep the Kestrel `MaxRequestBodySize` as the backstop for chunked / undeclared-length bodies.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: reject a declared-oversized request body with 413 before proxying.

## Impact
- **Code**: `src/DockYarp.App/ReverseProxy/RequestBodySizeMiddleware.cs`. Tests in
  `tests/DockYarp.IntegrationTests/RequestBodySizeMiddlewareTests.cs`.
- **Deferred**: softening the YARP forwarder `warn` for the streamed-body path (optional follow-up).
- **Owning agent**: AG-RP.
