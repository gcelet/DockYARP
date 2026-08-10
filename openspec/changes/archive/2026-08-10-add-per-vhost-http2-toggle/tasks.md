## 1. Labels & parsing (AG-DD)
- [x] 1.1 `DockerLabels`: add `DOCKYARP_HTTP2` (native) + `com.github.nginx-proxy.nginx-proxy.http2.enable` (alias)
- [x] 1.2 `ContainerLabelConfig`: add `bool? Http2Enabled` (null = global default; distinct from backend `Http2`)
- [x] 1.3 `LabelParser` (both blocks): `Http2Enabled = ParseBool(native ?? nginx alias)`
- [x] 1.4 `LabelParserTests`: true/false/unset; native wins over the nginx alias

## 2. Model, mapping & resolver (AG-AT)
- [x] 2.1 `HostTlsMetadata`: add `bool? Http2Enabled`
- [x] 2.2 `ContainerMapper`: carry `Http2Enabled` into the host TLS metadata in both blocks (no ACME desire)
- [x] 2.3 New `HostHttp2Resolver.Resolve(RouteConfigSnapshot, host) → bool?` (mirror `HostSslPolicyResolver`)
- [x] 2.4 `HostHttp2ResolverTests`: resolves for a matching host; null when unset / no match

## 3. Handshake ALPN (AG-AT)
- [x] 3.1 `SniTlsHandshakeCallback`: prepare `http1OnlyProtocols` once; `BuildOptions` uses it when the host
      disables HTTP/2, else the global `applicationProtocols`
- [x] 3.2 `SniTlsHandshakeCallbackTests`: disabled host → `http/1.1`-only ALPN; default host → global; enable while
      global `Http1` → still `http/1.1` (no-op)

## 4. Docs (AG-DOC)
- [x] 4.1 docs site `configuration.md`: document the `DOCKYARP_HTTP2` toggle + nginx alias row
- [x] 4.2 `docs/labels-reference.md`: add the new label row + alias mapping

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green (365 unit/integration tests), warnings-as-errors clean (`dotnet build` 0/0)
- [x] 5.2 Runtime ALPN negotiation proven by e2e: new `echo-http1` backend (`DOCKYARP_HTTP2=false`) +
      `Http2ToggleTests` (default host → HTTP/2, disabled host → HTTP/1.1). Full suite green (28/28) — parity ✅
