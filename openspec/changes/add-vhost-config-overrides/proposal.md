## Why
nginx-proxy's key extensibility is mounted config snippets (`vhost.d/<host>`, `vhost.d/default`, location
overrides) that add headers, rewrites, etc. per vhost. DockYarp has no escape hatch for behavior not modeled by
a label/option. Since DockYarp is YARP, "raw nginx config" has no literal equivalent — the analog is a
**structured** per-host/global override.

## What Changes
- Add a **structured overrides** section to the static configuration: per-host and global (`default`)
  **response-header** injections — the YARP-native analog of `vhost.d/<host>` and `vhost.d/default`.
- Overrides are applied to the merged routes (from discovery *and* static config): a host-specific override
  wins; otherwise the `default` override applies. Injected headers become YARP `ResponseHeader` transforms.
- **Replace-generated-route** is already available: a static-config route with the same host/path replaces the
  discovered one via the existing source precedence (static > discovery). This change documents and tests it.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: operators can inject per-host / global response headers and replace a generated route
  via structured overrides.

## Impact
- **Code**: `DockYarp.Core` — `RouteTransforms.ResponseHeaders`, a `ConfigOverrides` model, a pure
  `RouteOverrideApplier`, `IStaticConfigProvider.GetOverrides()` (default returns empty). `DockYarp.App` —
  parse the `overrides` section, apply after merge in `StaticConfigService`, emit `ResponseHeader`
  (`Set`, `When=Always`) transforms in `YarpConfigMapper`. `DockYarp.Docker` — apply after merge in
  `DiscoveryReconciler`.
- **Tests**: `RouteOverrideApplier` (per-host, default fallback, merge with existing transforms, empty →
  unchanged), `YarpConfigMapper` (response headers → YARP transforms), `StaticConfigProvider` (parse overrides),
  route replacement via precedence.
- **Scope guard**: this deliberately starts with **response headers + replace-route**; request-header
  injection, arbitrary transforms, and per-route metadata are structured extensions for later (not raw nginx).
- **Owning agent**: AG-RP. Resolves `add-vhost-config-overrides` (initial structured scope).
