## Why

nginx-proxy writes an access log for every request. DockYarp logs discovery, TLS, and errors but never
the requests it serves, so operators have no per-request visibility (status, latency, host/path).

## What Changes

- Add an **access-log middleware** that emits a structured log entry for each handled request — method,
  scheme, host, path, response status, and elapsed time — placed first in the pipeline so it covers proxied
  requests, redirects, and unmatched responses alike.
- The entry is **structured** (named fields via source-generated logging); text vs JSON rendering follows
  the configured .NET logging provider. Access logging can be disabled via `AccessLog:Enabled`.

## Capabilities

### Modified Capabilities
- `admin-api`: the observability surface includes a per-request access log.

## Impact

- **Code**: `src/DockYarp.App/Observability` (`AccessLogOptions`, `AccessLog`, `AccessLogMiddleware`, a
  registration extension), `Program` wiring.
- **Deferred**: a bespoke combined/JSON log formatter (delegated to the logging provider) and
  request/response body/size logging.
- **Owning agent**: AG-AA.
