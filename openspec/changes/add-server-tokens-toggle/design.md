# Design — add-server-tokens-toggle

## Data path
`SERVER_TOKENS` follows the per-container config channel; unlike `HSTS`/`SSL_POLICY` it is **top-level on
`RouteRule`** (not `HostTlsMetadata`) because the `Server` header applies to HTTP and HTTPS responses, not only
TLS-configured hosts:

```
DockerLabels.ServerTokens ("SERVER_TOKENS")
  → LabelParser: ServerTokens = GetOrNull(config, SERVER_TOKENS)   (env wins via EffectiveConfig)
  → ContainerLabelConfig.ServerTokens
  → ContainerMapper: RouteRule { ServerTokens = ... }              (both classic + multiports, unconditional)
  → SecurityHeadersMiddleware (resolves the route via RouteLookup)
```

## Semantics
The `Server` header is suppressed globally at Kestrel; `Security:ServerHeader` emits a configured value. Per host:
- `SERVER_TOKENS=off` (or empty) → suppress the `Server` header for that host, overriding the global value.
- any other value, or unset → global behavior (emit `Security:ServerHeader` if set).

DockYarp is not nginx and has no server version to reveal, so nginx's `on`/`build` collapse to "use the global
value". This mirrors the existing per-host `HSTS=off` suppression in `SecurityHeadersMiddleware`.

## Middleware
`SecurityHeadersMiddleware` resolves the matched route once (via `RouteLookup`, already injected) and reuses it
for both the `Server` header decision and the existing HSTS resolution — no extra per-request lookup.

## Tests
- `LabelParser`: `SERVER_TOKENS` parsed into the config.
- `SecurityHeadersMiddleware`: with a global `ServerHeader`, a per-host `SERVER_TOKENS=off` drops the header
  while a host without the override keeps it.
