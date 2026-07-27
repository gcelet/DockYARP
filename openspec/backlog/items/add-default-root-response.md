---
id: add-default-root-response
capability: proxy-routing
agent: AG-RP
tier: A-structural
priority: low
status: backlog
nginx-proxy: DEFAULT_ROOT
provenance: this parity pass (untracked feature)
---

## Why
nginx-proxy's `DEFAULT_ROOT` customizes the fallback `location /` for unmatched requests with any nginx
`return` expression (a status, a redirect, or a body), and `none` suppresses the default so an operator can
supply their own. DockYarp only returns a configurable status code, so redirect-style or custom fallbacks are
not expressible.

## nginx-proxy behavior
- `DEFAULT_ROOT` sets the fallback `location /` `return` expression (default `404`). Supports `$scheme`,
  `$host`, `$request_uri` (escape `$$`). Value `none` suppresses generation so custom `vhost.d` config can
  provide the location.

## DockYarp today
`Routing:DefaultHost` selects a catch-all backend; `Routing:DefaultResponseStatusCode` returns a fixed status
for unmatched requests (`src/DockYarp.Core/Configuration/RoutingOptions.cs`). No redirect/body/`$var`
templating; no "suppress" mode.

## Proposed change (sketch)
Extend the default-response handling to accept either a status code (current) or a redirect target
(status + Location, with `$scheme`/`$host`/`$request_uri` substitution), plus a "none/passthrough" mode. Wire
into the default-response middleware/endpoint used when no route and no `DefaultHost` match.

## Acceptance criteria (→ scenarios)
- **WHEN** the default response is configured as a redirect to `https://$host$request_uri` and an unmatched
  request arrives **THEN** the client receives a redirect to the same host+path over HTTPS.
- **WHEN** the default response is a status code **THEN** behavior is unchanged.
- **WHEN** the default response is "none" and a `DefaultHost` is set **THEN** the `DefaultHost` still wins.

## Notes / risks / references
- Keep it config-driven (this is proxy-wide, not a per-container label in nginx-proxy).
