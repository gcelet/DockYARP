## Context

Requests are forwarded with YARP defaults: a 100s idle `ActivityTimeout` and no request body-size limit.
nginx-proxy tunes these per vhost (`proxy_read_timeout`, `client_max_body_size`). This adds a per-cluster
request timeout (mapped to YARP) and a per-route body-size limit (enforced by a small middleware).

## Goals / Non-Goals

**Goals:** a per-cluster request timeout via `DOCKYARP_PROXY_TIMEOUT`; a per-route request body-size
limit via `DOCKYARP_MAX_BODY_SIZE`, enforced before proxying.

**Non-Goals (deferred):** response buffering (YARP streams by default, which is desirable for SSE),
gzip/compression, a global Kestrel body limit, and WebSocket assertions (YARP proxies WebSockets by
default).

## Decisions

- **Model**: `Cluster.RequestTimeout` (`TimeSpan?`) and `RouteRule.MaxRequestBodySize` (`long?`), both
  optional/unset by default.
- **Labels**: parsed in `LabelParser` — timeout as positive integer seconds, body size as a positive
  `long` of bytes; unparseable/non-positive values are ignored, and pure `HasInvalid*` helpers let the
  mapper warn (parser stays pure). The mapper sets the cluster timeout and the route limit from the
  first-seen container config (consistent with LB policy / TLS).
- **YARP mapping**: `YarpConfigMapper.BuildCluster` sets `ClusterConfig.HttpRequest =
  new ForwarderRequestConfig { ActivityTimeout = timeout }` when a timeout is present (verified via
  Microsoft Learn).
- **Body-size enforcement**: a route-aware `RequestBodySizeMiddleware` (App) sets
  `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` from the matched route before the reverse proxy runs.
  A middleware is required because the limit is per-route while Kestrel's default is global.

## Risks / Trade-offs

- `ActivityTimeout` is an idle timeout (resets on activity), matching nginx's read timeout semantics closely
  enough; a total-request timeout (YARP route `Timeout`) is not configured here.
- The body-size middleware is a no-op when the server feature is absent (e.g. a test host without it),
  failing open — acceptable, since the limit is a safeguard rather than a security boundary.

## Migration Plan

Additive: two optional model fields, two labels, one YARP mapping branch, and one middleware. Nothing
changes unless a container sets the labels.

## Open Questions

- Per-vhost buffering/compression and a global body limit — deferred; revisit with runtime tuning needs.
