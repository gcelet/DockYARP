## Why
nginx-proxy's `DEFAULT_ROOT` customizes the fallback for unmatched requests with any `return` expression — a
status, or a **redirect** to a template using `$scheme`/`$host`/`$request_uri`. DockYarp only returns a fixed
status code, so a redirect-style default (e.g. bounce unknown hosts to a canonical site over HTTPS) is not
expressible.

## What Changes
- `RoutingOptions` gains `DefaultResponseLocation` (a redirect target template). When set, the unmatched-request
  fallback issues a redirect using `DefaultResponseStatusCode` as the redirect status and the substituted
  `Location`; when unset, it returns the status code as today.
- Template substitution supports `$scheme`, `$host`, `$request_uri`, and `$$` (literal `$`).
- A small `DefaultResponseWriter` replaces the inline `MapFallback` status write.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: the response for unmatched requests may be a redirect (templated `Location`), not only a
  fixed status code.

## Impact
- **Code**: `DockYarp.Core` (`RoutingOptions.DefaultResponseLocation`), `DockYarp.App` (`DefaultResponseWriter`,
  `Program.cs` fallback wiring).
- **Tests**: `DefaultResponseWriter` — status-only unchanged; redirect with `$host`/`$request_uri` substitution;
  `$$` escape.
- **Docs**: `docs/deployment.md` config table (`Routing` row).
- **Scope note**: nginx's `DEFAULT_ROOT=none` (suppress the default so custom `vhost.d` config serves it) is not
  included — it only makes sense once raw per-vhost config exists (`add-vhost-config-overrides`); a configured
  `DefaultHost` still wins over the fallback regardless.
- **Owning agent**: AG-RP. Resolves `add-default-root-response`.
