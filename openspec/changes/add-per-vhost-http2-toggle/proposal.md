## Why
nginx-proxy lets a single vhost enable/disable HTTP/2 via the
`com.github.nginx-proxy.nginx-proxy.http2.enable` label, overriding the global `ENABLE_HTTP2`. DockYarp's HTTP/2 is
a **global** listener setting (`Tls:HttpProtocols`), so one host cannot opt out of HTTP/2 (e.g. a backend that
misbehaves over h2) while others keep it. This closes that per-vhost parity gap.

## What Changes
- Recognize a **per-host HTTP/2 toggle** — the DockYarp-native `DOCKYARP_HTTP2` label or the nginx-proxy
  `com.github.nginx-proxy.nginx-proxy.http2.enable` alias (boolean) — carried on the host's TLS metadata.
- In the per-connection SNI handshake, advertise **only HTTP/1.1 via ALPN** for a host that disables HTTP/2; a host
  that leaves it unset keeps the globally-configured protocols.
- Because HTTP/2 is bound at the HTTPS listener from the **global** protocol set, the toggle only **narrows** the
  offered protocols (disabling h2 for a host); enabling it beyond the global set has no effect (documented).

## Capabilities
### Modified Capabilities
- `tls-acme`: the per-connection TLS handshake honors a per-host HTTP/2 toggle when assembling the offered ALPN.

## Impact
- **Code**: `DockerLabels`, `ContainerLabelConfig`, `LabelParser` (label + alias parsing); `HostTlsMetadata`,
  `ContainerMapper` (carry the flag); new `HostHttp2Resolver` (mirror of `HostSslPolicyResolver`);
  `SniTlsHandshakeCallback` (per-host ALPN selection). Unit-tested throughout.
- **Docs (user-facing)**: docs site `configuration.md` + `docs/labels-reference.md` (new label).
- **Distinct from backend HTTP/2**: `ContainerLabelConfig.Http2` (from `VIRTUAL_PROTO=grpc`) drives the *cluster*'s
  `Http2Only` (proxy→backend); this change is the *frontend* (client→proxy) ALPN offering — unrelated.
- **Owning agent**: AG-AT. Runtime ALPN negotiation is proven by an e2e (new `echo-http1` backend +
  `Http2ToggleTests`, full suite 28/28) — parity ✅.
