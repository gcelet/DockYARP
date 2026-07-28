## Context
`KestrelTlsConfigurator.Configure` (`src/DockYarp.Tls/KestrelTlsConfigurator.cs:58-59`) sets
`serverOptions.ConfigureEndpointDefaults(listen => listen.Protocols = TlsHardening.ParseHttpProtocols(options.HttpProtocols))`.
`ConfigureEndpointDefaults` applies to **every** listener, so the plaintext `8080` endpoint also gets the
default `Http1AndHttp2`; Kestrel then warns because HTTP/2 needs TLS. The endpoints are ports-only
(`ASPNETCORE_HTTP_PORTS=8080`, `ASPNETCORE_HTTPS_PORTS=8443`, set in the Dockerfile).

## Goals / Non-Goals
- **Goal**: the plaintext HTTP endpoint negotiates HTTP/1.1 only; the HTTPS endpoint keeps its configured
  protocols. Warning gone.
- **Non-Goal**: the Data Protection warnings (separate item); reworking the ports-only listener model.

## Decisions
- Resolve the plaintext HTTP port from `ASPNETCORE_HTTP_PORTS`; in `ConfigureEndpointDefaults`, set
  `HttpProtocols.Http1` **only** for the endpoint whose `IPEndPoint.Port` matches it, and keep
  `ParseHttpProtocols(options.HttpProtocols)` for all others (the HTTPS endpoint).
- Match on the **HTTP** port (not "not the HTTPS port") so the HTTPS endpoint is never accidentally downgraded:
  if the port is unknown or `IPEndPoint` is null, behavior is unchanged (configured protocols everywhere).

## Risks / Trade-offs
- If `IPEndPoint` is not populated in `ConfigureEndpointDefaults` for a ports-only endpoint, the HTTP endpoint
  simply keeps its current protocols (warning remains) — no functional regression. Confirmed at the e2e run.

## Migration Plan
- None; the HTTP endpoint already served HTTP/1.1 (this only stops advertising HTTP/2 on it).

## Open Questions
- None.
