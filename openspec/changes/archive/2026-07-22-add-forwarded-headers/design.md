## Context

YARP defaults suppress the incoming `Host` and set `X-Forwarded-For/Proto/Host/Prefix` with action `Set`
(replacing any client-supplied values). DockYarp relies on those defaults without asserting them, sets
no `X-Real-IP`/`X-Forwarded-Port`, and does not forward the original `Host` — unlike nginx-proxy.

## Goals / Non-Goals

**Goals:** explicit, configurable forwarded headers (`X-Forwarded-For/Proto/Host/Port`, `X-Real-IP`),
forward the original `Host`, and a downstream-proxy trust switch.

**Non-Goals:** PROXY protocol, `Forwarded` header, per-route header tuning.

## Decisions

- **Global transforms** via `AddReverseProxy().AddTransforms(ctx => ForwardedHeadersTransform.Apply(ctx, trust))`.
- **Trust switch** `Proxy:TrustDownstreamProxy` (default `true`): trusted ⇒ `AddXForwarded(Append)` (append to
  the client's existing chain); untrusted ⇒ `AddXForwarded(Set)` (overwrite with the connection's values).
  This mirrors nginx-proxy's `TRUST_DOWNSTREAM_PROXY`. `UseDefaultForwarders = false` first so our config is
  authoritative.
- **Original Host forwarded** via `AddOriginalHost(true)` (YARP suppresses it by default) — nginx-proxy sets
  `Host $host`, and many backends rely on it for virtual hosting.
- **`X-Real-IP` and `X-Forwarded-Port`** are not built into YARP: add them with a custom
  `AddRequestTransform` from `Connection.RemoteIpAddress` and `Connection.LocalPort`.

## Risks / Trade-offs

- Forwarding the original Host changes what backends see (vs YARP default) — intended for parity; the
  destination address is still the cluster endpoint, so routing is unaffected.
- `X-Real-IP`/`X-Forwarded-For` depend on `RemoteIpAddress`, which the in-memory test server may not
  populate; the integration test asserts the connection-independent headers (`Proto`, `Host`,
  `X-Forwarded-Host`) to stay reliable.

## Migration Plan

Additive: a transform helper, one builder call, and a config-driven trust flag.

## Open Questions

- Whether to also honor inbound `X-Forwarded-Port`/`X-Real-IP` in trusted mode — kept simple (always set
  from the connection); revisit if a real multi-hop deployment needs it.
