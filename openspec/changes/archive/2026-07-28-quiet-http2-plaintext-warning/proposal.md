## Why
At startup Kestrel warns: *"HTTP/2 is not enabled for [::]:8080… TLS is not enabled… Connections will use
HTTP/1.1."* The plaintext HTTP endpoint (ACME challenges + redirects) is configured for `Http1AndHttp2` because
`KestrelTlsConfigurator` applies the configured protocols to **every** endpoint via `ConfigureEndpointDefaults`.
HTTP/2 requires TLS, so the plaintext endpoint falls back to HTTP/1.1 anyway — the warning is pure noise.

This is the HTTP/2 half of the `quiet-startup-warnings` backlog item; the Data Protection warnings are deferred
to their own item (DockYarp does not use Data Protection; removing it cleanly needs separate investigation).

## What Changes
- Force the plaintext HTTP endpoint to `HttpProtocols.Http1`; keep the configured protocols
  (`Http1AndHttp2`) on the HTTPS endpoint. Distinguish by the `ASPNETCORE_HTTP_PORTS` port.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the plaintext HTTP endpoint negotiates HTTP/1.1 only (no spurious HTTP/2-without-TLS warning).

## Impact
- **Code**: `src/DockYarp.Tls/KestrelTlsConfigurator.cs`.
- **Deferred**: the Data Protection ephemeral/unencrypted-keys warnings → new backlog item
  `remove-unused-data-protection`.
- **Owning agent**: AG-AT.
- **Runtime**: verified by an `E2E` run — the `dockyarp.log` (e2e diagnostics) should no longer contain the
  `Microsoft.AspNetCore.Server.Kestrel[64]` HTTP/2 warning, and 8443 keeps HTTP/1.1 + HTTP/2.
