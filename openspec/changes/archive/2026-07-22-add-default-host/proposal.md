## Why

nginx-proxy designates a `DEFAULT_HOST` (served for unmatched host names) and a configurable default
response (`DEFAULT_ROOT`) for requests that match nothing. DockYarp currently returns YARP's plain 404
for any unmatched host, with no catch-all or default host.

## What Changes

- Add a **default host**: a configurable host whose route also serves requests whose host matches no other
  route (catch-all), so a chosen backend handles unknown hosts.
- Add a **configurable default response** for genuinely unmatched requests (e.g. `404`, `503`, or a
  redirect) instead of a bare 404.

## Capabilities

### Modified Capabilities
- `proxy-routing`: matching supports a default (catch-all) host selection.
- `yarp-dynamic-config`: unmatched requests produce the configured default response.

## Impact

- **Code**: `src/DockYarp.Core` (matcher default-host fallback), `src/DockYarp.App/ReverseProxy`
  (default response / catch-all route), configuration for the default host and response.
- **Owning agent**: AG-RP.
