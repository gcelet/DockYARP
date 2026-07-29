## 1. Register compression (AG-RP)
- [x] 1.1 `Program.cs`: read `Compression:Enabled` (default true); when enabled, `AddResponseCompression`
      (Brotli + Gzip, `EnableForHttps=true`, providers at `Fastest`)
- [x] 1.2 `Program.cs`: `UseResponseCompression()` after the access-log middleware, before the rest of the pipeline

## 2. Docs (AG-DEP)
- [x] 2.1 `docs/deployment.md`: add `Compression:Enabled` to the config table

## 3. Tests (AG-RP)
- [x] 3.1 Integration: `GET /metrics` with `Accept-Encoding: gzip` → `Content-Encoding: gzip` (default on)
- [x] 3.2 Integration: with `Compression:Enabled=false` → no `Content-Encoding`

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
