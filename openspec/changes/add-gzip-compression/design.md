# Design — add-gzip-compression

## Approach
Use ASP.NET Core's built-in `ResponseCompressionMiddleware` rather than a custom transform: it already handles
`Accept-Encoding` negotiation, the compressible-MIME allow-list, skipping responses that already carry a
`Content-Encoding`, and streaming (it does not buffer the whole body). This covers proxied responses because
YARP writes the response body through the pipeline's body stream, which the compression middleware wraps.

## Registration (Program.cs)
Gated on `Compression:Enabled` (default `true`):
- `AddResponseCompression` with `EnableForHttps = true` and the Brotli + Gzip providers (default compressible
  MIME types: `text/*`, `application/json`, `application/xml`, `application/wasm`, JavaScript…).
- Both providers set to `CompressionLevel.Fastest` — a reverse proxy favors latency/throughput over maximum
  ratio, and it avoids Brotli's very slow top level.
- `UseResponseCompression()` is placed immediately after the access-log middleware (which stays outermost for
  logging) and before everything that writes a response (ACME, security, proxy, admin, metrics, fallback), so
  all downstream responses flow through it.

When `Compression:Enabled` is `false`, neither the service nor the middleware is registered and responses pass
through unchanged.

## Not double-compressing
`ResponseCompressionMiddleware` skips any response that already has a `Content-Encoding` header, so a backend
that returns an already-compressed body is passed through untouched — no configuration needed.

## HTTPS / BREACH
Compressing over HTTPS re-introduces the theoretical BREACH risk for responses that reflect attacker-controlled
input alongside secrets. nginx-proxy compresses over HTTPS by default; DockYarp matches that (`EnableForHttps =
true`) for parity, and it can be turned off entirely via `Compression:Enabled=false`. Documented as a trade-off.

## Testing
Integration tests hit `/metrics` (a `text/plain` endpoint, no backend needed):
- with `Accept-Encoding: gzip` and compression on → response carries `Content-Encoding: gzip`;
- with `Compression:Enabled=false` → no `Content-Encoding`.
The test client does not auto-decompress, so the `Content-Encoding` header is observable.
