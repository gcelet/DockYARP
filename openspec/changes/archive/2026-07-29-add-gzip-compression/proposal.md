## Why
nginx-proxy enables gzip response compression by default — an expected front-proxy behavior. DockYarp serves
responses uncompressed, so text payloads are larger over the wire than with nginx-proxy.

## What Changes
- Register ASP.NET Core response compression (Brotli + Gzip) for the default compressible content types, placed
  early in the pipeline so it compresses proxied responses.
- **On by default** (matching nginx-proxy), toggle-able via `Compression:Enabled`.
- Respect the upstream `Content-Encoding` (the middleware never double-compresses an already-encoded response).
- Compress over HTTPS as well (parity with nginx-proxy), at the `Fastest` level to keep proxy latency low.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: proxied responses are compressed (gzip/brotli) for compressible content types when the
  client accepts it, unless disabled or already encoded.

## Impact
- **Code**: `src/DockYarp.App/Program.cs` (register + use response compression, gated on `Compression:Enabled`).
- **Tests**: `DockYarp.IntegrationTests` — a compressible endpoint (`/metrics`) is gzip-encoded when the client
  sends `Accept-Encoding: gzip`, and is not encoded when compression is disabled by config.
- **Docs**: `docs/deployment.md` config table (`Compression:Enabled`).
- **Notes**: compression streams (no full-response buffering); enabling it over HTTPS carries the usual
  BREACH trade-off, accepted here for nginx-proxy parity and documented.
- **Owning agent**: AG-RP. Resolves `add-gzip-compression`.
