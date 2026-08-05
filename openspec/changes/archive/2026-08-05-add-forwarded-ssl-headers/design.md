# Design — add-forwarded-ssl-headers

## Where
Both headers are added in the existing request transform in `ForwardedHeadersTransform.Apply` (alongside
`X-Real-IP`, `X-Forwarded-Port`, and the httpoxy `Proxy` strip), which runs **after** `AddXForwarded`, so
`X-Forwarded-Proto` is already present on the proxied request.

## X-Forwarded-Ssl (`on`/`off`)
Must agree with `X-Forwarded-Proto` and respect the downstream-proxy trust mode. Rather than re-derive the
scheme, read the **first hop** of the `X-Forwarded-Proto` header YARP just set:
- `Set` mode (trust off) → one value = DockYarp's own scheme (`Request.Scheme`).
- `Append` mode (trust on) → the client's original proto is the first hop.
```
https = firstHop(X-Forwarded-Proto) == "https"   (fallback: HttpContext.Request.IsHttps)
X-Forwarded-Ssl = https ? "on" : "off"     (removed then set — derived, never appended)
```

## X-Original-URI
The original request line, before any route rewrite: `Request.PathBase + Request.Path + Request.QueryString`.
YARP transforms rewrite `ProxyRequest.RequestUri`, not `HttpContext.Request`, so the original URI is intact
here. Always the real URI (removed then set — a client cannot spoof it), independent of trust mode.

## Out of scope
- Any change to `X-Forwarded-For`/`-Proto`/`-Host`/`-Port` / `X-Real-IP` (unchanged).
- nginx's `$host_port` Host-header port suffix (a separate niche behavior).
