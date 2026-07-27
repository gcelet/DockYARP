---
id: add-gzip-compression
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: gzip (nginx.tmpl default)
provenance: this parity pass (matrix: gzip ⛔)
---

## Why
nginx-proxy enables gzip response compression by default (a common expectation for a front proxy). DockYarp
streams responses without compression, so text payloads are larger over the wire than with nginx-proxy.

## nginx-proxy behavior
- gzip is on by default in the template with a broad `gzip_types` set (`nginx.tmpl:540`); not env-configurable.

## DockYarp today
No response compression (matrix ⛔). YARP streams responses; ASP.NET Core `ResponseCompression` middleware is
not registered in `src/DockYarp.App`.

## Proposed change (sketch)
Register ASP.NET Core response compression (Brotli + Gzip) for compressible content types, placed so it
compresses proxied responses that are not already compressed. Make it toggle-able (default on, matching
nginx-proxy) and respect upstream `Content-Encoding`.

## Acceptance criteria (→ scenarios)
- **WHEN** a client sends `Accept-Encoding: gzip` and the backend returns compressible text without
  `Content-Encoding` **THEN** the response is gzip-compressed.
- **WHEN** the backend already returns a compressed body **THEN** DockYarp does not double-compress.
- **WHEN** compression is disabled by config **THEN** responses pass through uncompressed.

## Notes / risks / references
- Verify interaction with YARP streaming/`Content-Length`; avoid buffering large responses unnecessarily.
