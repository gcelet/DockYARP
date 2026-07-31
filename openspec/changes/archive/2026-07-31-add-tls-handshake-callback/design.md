# Design — add-tls-handshake-callback

## Goal
Introduce a per-connection TLS assembly point (SNI-keyed) with **zero behavior change**, replacing the single
global `ConfigureHttpsDefaults` posture. This is the enabler for per-vhost `SSL_POLICY`
(`add-per-vhost-ssl-policy`); it ships no new user-facing knob.

## Decision 1 — callback API: `TlsHandshakeCallbackOptions.OnConnection`
Two APIs receive the SNI host and return a full `SslServerAuthenticationOptions`; both bypass
`ConfigureHttpsDefaults` and the default certificate (confirmed via microsoft-docs, aspnetcore-10.0):

- `ServerOptionsSelectionCallback` (System.Net.Security) — low-level `(SslStream, SslClientHelloInfo, state, ct)`.
- `TlsHandshakeCallbackOptions.OnConnection` (Kestrel) — `TlsHandshakeCallbackContext` exposes
  `ClientHelloInfo.ServerName`, the `ConnectionContext`, `State`, and `AllowDelayedClientCertificateNegotation`.

**Chosen: `TlsHandshakeCallbackOptions`.** It is the ASP.NET-integrated surface (connection + DI state access,
delayed client-cert negotiation for mTLS), attaches via `listenOptions.UseHttps(callbackOptions)`, and keeps the
door open for optional client certificates later. The lower-level delegate offers no advantage here.

## Decision 2 — the App owns its data-plane endpoints
The callback attaches to a specific `ListenOptions` via `UseHttps`, so binding can no longer come from
host-injected `ASPNETCORE_URLS`/`ASPNETCORE_HTTPS_PORTS`. The App binds in code:

```
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.ListenAnyIP(httpPort,  o => o.Protocols = HttpProtocols.Http1);          // plaintext: ACME + redirects
    k.ListenAnyIP(httpsPort, o => o.UseHttps(tlsCallbackOptions));             // TLS: per-SNI assembly
});
```

- Ports from new config `Server:HttpPort` (default `8080`) / `Server:HttpsPort` (default `8443`) — matching
  DockYarp's non-root chiseled container convention (the orchestrator maps host 80/443 onto them). This is the
  port pair the Dockerfile already sets via `ASPNETCORE_HTTP_PORTS`/`ASPNETCORE_HTTPS_PORTS`; those are replaced
  by `Server__*` env, and since the defaults match, **the Aspire e2e AppHost needs no change**.
- **No double-bind**: once endpoints are configured in code, Kestrel ignores `ASPNETCORE_URLS` and logs a benign
  `Overriding address(es) '…'. Binding to endpoints defined in ConfigureKestrel instead.` This is the documented
  precedence, so Aspire's injected URLs are harmlessly overridden.
- The `tlsCallbackOptions` is resolved from DI (it closes over `SniCertificateSelector`, the global
  `SslPolicyResolution`, `ClientCertificateValidator`, and `TlsOptions`). Since `ConfigureKestrel` runs before
  the container is built, the App resolves these from a temporary provider **or** the binding is moved into a
  small `IConfigureOptions<KestrelServerOptions>` (as today `KestrelTlsConfigurator` is) that receives them via
  DI and calls `serverOptions.ListenAnyIP(...)`. **Preferred: keep it in `KestrelTlsConfigurator`** (already an
  `IConfigureOptions<KestrelServerOptions>` with the right dependencies injected) and have it define the
  endpoints, so `Program.cs` only supplies `AddServerHeader = false`.

## Decision 3 — `SniTlsHandshakeCallback` assembles the session (behavior-preserving)
Per connection, keyed by `ctx.ClientHelloInfo.ServerName`:

| Facet | Source (unchanged) |
|---|---|
| `ServerCertificate` | `SniCertificateSelector.Select(host)` (CERT_NAME → exact → wildcard-parent → fallback) |
| `EnabledSslProtocols` | `TlsHardening.ToSslProtocols(global.MinimumTlsVersion)` |
| `CipherSuitesPolicy` | global ciphers via `TlsHardening.ParseCipherSuites` — **Linux/macOS only** (guarded), else omitted |
| `ClientCertificateRequired` + `RemoteCertificateValidationCallback` | when `ClientCertificateValidator.HasClientCa`, request + validate chain to CA |
| ALPN / HTTP protocols | HTTPS endpoint keeps configured `HttpProtocols` (`Http1AndHttp2`); HTTP endpoint pinned to `Http1` |

The **global** `SslPolicyResolution` is resolved once at startup (`SslPolicyPresets.Resolve`) and captured; only
the certificate lookup runs per handshake. `add-per-vhost-ssl-policy` later swaps the single global resolution
for a per-host lookup at this exact point.

## Hot path & allocation
- Precompute and cache the immutable pieces (protocols, cipher policy, mTLS delegate). Build one
  `SslServerAuthenticationOptions` per connection (unavoidable — the API is per-connection); reuse cached
  sub-objects. Use a `static` validation lambda closing over the captured validator. Return `ValueTask` from the
  callback without extra async state where possible.
- `HandshakeTimeout`: keep Kestrel's default (10 s) via `TlsHandshakeCallbackOptions.HandshakeTimeout`.

## Behavior-preservation checklist (what the existing e2e regression must still pass)
- SNI serves the right cert (step-ca issued) — existing e2e.
- mTLS: cert chaining to CA accepted, otherwise rejected — existing e2e + unit tests.
- TLS version floor / cipher policy unchanged — unit tests (Linux for ciphers).
- HTTP/2 over TLS still negotiated; ACME HTTP-01 + redirects served on the plaintext port.

## Open questions / risks
- **Aspire endpoint match**: the e2e AppHost declares `targetPort` 8080/8443; the App must bind those exact
  ports → pass `Server__HttpPort=8080` / `Server__HttpsPort=8443` in the AppHost. Verify no health-check probes
  assume the old URL binding.
- **`AddServerHeader`** stays `false`. mTLS `DelayCertificate`/renegotiation is **out of scope** here (kept as
  today's `AllowCertificate` semantics); optional-client-cert per host is a separate item.
- If moving endpoint definition into `KestrelTlsConfigurator` proves awkward with the HTTP/HTTPS port config
  source, fall back to binding directly in `Program.cs` resolving the callback deps from `builder.Services`.
