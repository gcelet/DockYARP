## Why
nginx-proxy's `SERVER_TOKENS` is a per-vhost knob for the `Server` response header. DockYarp suppresses the
`Server` header globally and can emit a configured value via `Security:ServerHeader`, but a single host cannot
opt out of that global value. This closes the per-container gap.

## What Changes
- `SERVER_TOKENS` becomes a recognized per-container key (env var or label; environment wins via `EffectiveConfig`).
- A host that declares `SERVER_TOKENS=off` (or empty) has its `Server` header suppressed, overriding the global
  `Security:ServerHeader`; any other value keeps the global behavior (DockYarp has no server version to reveal,
  so nginx's `on`/`build` map to "use the global value").

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: the `Server` response header can be suppressed per host via `SERVER_TOKENS=off`, overriding the
  global configured value.

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels.ServerTokens`, `ContainerLabelConfig.ServerTokens`, `LabelParser`
  reads it, `ContainerMapper` carries it into `RouteRule.ServerTokens` (both classic + multiports).
  `DockYarp.Core` — `RouteRule.ServerTokens`. `DockYarp.Security` — `SecurityHeadersMiddleware` suppresses the
  `Server` header for a host declaring `SERVER_TOKENS=off`.
- **Tests (unit)**: `LabelParser` parses `SERVER_TOKENS`; `SecurityHeadersMiddleware` suppresses the header for a
  per-host `off` and keeps the global value otherwise.
- **Runtime / e2e**: none — response-header behavior is fully unit-testable.
- **Owning agent**: AG-SEC. Resolves `add-server-tokens-toggle`.
