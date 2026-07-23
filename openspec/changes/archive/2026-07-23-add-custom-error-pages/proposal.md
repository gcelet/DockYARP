## Why

nginx-proxy can serve custom error pages (`error_page`). DockYarp returns bodiless status responses for
the errors it generates itself — the unmatched/default response (404/503), the `nohttps` refusal (404),
client-certificate (403) and Basic Auth (401) — so operators cannot brand or explain those responses.

## What Changes

- Load HTML error pages named `{statusCode}.html` from a configured directory (`ErrorPages:Directory`) via
  the filesystem abstraction.
- A middleware, wrapping the pipeline, writes the matching page as the response body when DockYarp
  produced an error status (≥ 400) with no body yet (the response has not started). Responses already
  streamed by a backend are left untouched.

## Capabilities

### Modified Capabilities
- `yarp-dynamic-config`: DockYarp-generated error responses can carry a configured HTML body.

## Impact

- **Code**: `src/DockYarp.App` (`ErrorPagesOptions`, `ErrorPageProvider` over `IFileSystem`,
  `ErrorPageMiddleware`, wiring).
- **Deferred**: rewriting bodies of already-streamed backend error responses (would require response
  buffering), and per-host error pages.
- **Owning agent**: AG-AA / AG-RP.
