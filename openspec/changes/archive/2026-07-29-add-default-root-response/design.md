# Design — add-default-root-response

## Fallback response
The terminal `MapFallback` (reached only when no route and no `DefaultHost` match) delegates to a
`DefaultResponseWriter`:
- **`DefaultResponseLocation` unset** → set `DefaultResponseStatusCode` (default 404). Unchanged behavior.
- **`DefaultResponseLocation` set** → set `DefaultResponseStatusCode` (a 3xx, e.g. 302) and a `Location` header
  built from the template. This expresses nginx's `return <status> <url>`.

Because the `DefaultHost` route (and all discovered routes) are matched before the fallback, a configured
`DefaultHost` always wins over this default response — the fallback is strictly last.

## Template substitution
`$scheme`, `$host`, `$request_uri` are replaced with the request's scheme, host (with port), and path+query.
`$$` yields a literal `$` — handled by swapping `$$` to a sentinel first, substituting the variables, then
restoring the sentinel, so a `$$host` in the template is not treated as a variable.

## Config-driven
This is proxy-wide configuration (nginx-proxy's `DEFAULT_ROOT` is not a per-container label), so it lives on
`RoutingOptions` (the `Routing` section), consistent with `DefaultHost` and `DefaultResponseStatusCode`.

## Testing
`DefaultResponseWriter` is a pure function over `HttpContext` + `RoutingOptions`, unit-tested with a
`DefaultHttpContext`: status-only returns the status with no `Location`; a redirect template yields the status
and a substituted `Location` (`https://$host$request_uri` → `https://app.local/x?a=1`); `$$` escapes to `$`.
The `MapFallback` wiring is a one-liner and is not separately integration-tested.
