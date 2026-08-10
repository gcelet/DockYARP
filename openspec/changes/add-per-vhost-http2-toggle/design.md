# Design — add-per-vhost-http2-toggle

## Scope: frontend ALPN, not backend transport
Two unrelated HTTP/2 notions exist; keep them distinct:
- **Backend** (already present): `ContainerLabelConfig.Http2` (from `VIRTUAL_PROTO=grpc`/`grpcs`) → the cluster's
  `Http2Only` (how YARP talks to the container). **Not touched.**
- **Frontend** (this change): whether the HTTPS listener offers HTTP/2 to the *client* for a host, via the ALPN
  list in the per-connection handshake.

## The restriction-only model (honest, matches the listener)
`KestrelTlsConfigurator` binds the HTTPS endpoint with `listen.Protocols = ParseHttpProtocols(Tls:HttpProtocols)`
(default `Http1AndHttp2`). The endpoint can only *process* protocols in that global set. Therefore the per-host
toggle can only **narrow** what a host advertises:
- toggle **false** → advertise `http/1.1` only (a strict subset — always safe);
- toggle **true** or **unset** → the global ALPN list (whatever the listener supports).

Setting the toggle **true** while HTTP/2 is disabled globally is a **no-op** (the listener would not process h2, so
we never advertise it). This is documented rather than faked.

## Wiring (mirrors the SSL_POLICY per-host path)
1. **Labels** (`DockerLabels`): `DOCKYARP_HTTP2` (native) + `com.github.nginx-proxy.nginx-proxy.http2.enable`
   (nginx-proxy alias).
2. **Parse** (`LabelParser`, both the multiport and single-host blocks):
   `Http2Enabled = ParseBool(GetOrNull(DOCKYARP_HTTP2) ?? GetOrNull(nginx alias))` → `ContainerLabelConfig.Http2Enabled`
   (`bool?`, null = default). Reuses the existing `ParseBool`.
3. **Model + map**: add `bool? Http2Enabled` to `HostTlsMetadata`; `ContainerMapper` copies it into the host TLS
   metadata in both construction blocks (alongside `SslPolicy`). Like the other per-host TLS attributes it does
   **not** create an ACME certificate desire.
4. **Resolve**: new pure `HostHttp2Resolver.Resolve(RouteConfigSnapshot, host) → bool?` (mirror of
   `HostSslPolicyResolver`), returning the matching host's `Tls.Http2Enabled`.
5. **Handshake** (`SniTlsHandshakeCallback`): prepare a `http1OnlyProtocols` list once
   (`[SslApplicationProtocol.Http11]`); in `BuildOptions`, when the resolver returns `false`, set
   `ApplicationProtocols = http1OnlyProtocols`, otherwise keep the global `applicationProtocols`. No per-handshake
   allocation.

## Tests
- `LabelParserTests`: `DOCKYARP_HTTP2` / nginx alias → `Http2Enabled` true/false/unset (native wins over alias).
- `HostHttp2ResolverTests` (new): resolves the flag for a matching host; null when unset/no match.
- `SniTlsHandshakeCallbackTests`: `BuildOptions` advertises `http/1.1` only when a host disables h2; the global list
  otherwise; and the enable-beyond-global no-op (global `Http1` + host true → still `http/1.1`).

## Out of scope
- Backend `Http2Only` (unchanged). HTTP/3 per-vhost (`http3.enable`) folds into `finish-http3`.
- Proving the actual wire negotiation is covered here by an e2e (new `echo-http1` backend with
  `DOCKYARP_HTTP2=false` + `Http2ToggleTests` asserting the negotiated HTTP version); full suite 28/28.
