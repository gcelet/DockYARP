## Why

The access log records **every** request at Information, including DockYarp's own infrastructure
endpoints — `/metrics` (scraped frequently by Prometheus) and `/api/*` (admin). This floods the log with
noise unrelated to proxied traffic.

## What Changes

- Add `AccessLog:ExcludedPathPrefixes` (default `["/metrics", "/api"]`); the access-log middleware skips a
  request whose path starts with an excluded prefix (segment-aware).

## Capabilities

### Modified Capabilities
- `admin-api`: access logging excludes configured infrastructure path prefixes.

## Impact

- **Code**: `src/DockYarp.App/Observability` (`AccessLogOptions`, `AccessLogMiddleware`).
- **Owning agent**: AG-AA.
