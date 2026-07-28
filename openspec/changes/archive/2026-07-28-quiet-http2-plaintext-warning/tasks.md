## 1. Endpoint protocol (AG-AT)
- [x] 1.1 In `KestrelTlsConfigurator`, resolve the plaintext HTTP port from `ASPNETCORE_HTTP_PORTS` and, in
      `ConfigureEndpointDefaults`, set `HttpProtocols.Http1` for that endpoint only; keep the configured
      protocols for all others (the HTTPS endpoint)

## 2. Verify (AG-AT)
- [x] 2.1 Build green (compile-validated); to confirm at the next `E2E` run that `dockyarp.log` no longer
      contains the `Microsoft.AspNetCore.Server.Kestrel[64]` HTTP/2-without-TLS warning and 8443 still serves
      requests
