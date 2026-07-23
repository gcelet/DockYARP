## Why

nginx-proxy exposes proxy tuning knobs per vhost — notably `client_max_body_size` and proxy timeouts.
DockYarp forwards with YARP defaults: no per-cluster request timeout and no per-route request body-size
limit, so a hung backend ties up a request until the 100s default and large uploads are unbounded.

## What Changes

- **Per-cluster request timeout** (`DOCKYARP_PROXY_TIMEOUT`, seconds) → the cluster's YARP
  `ActivityTimeout`, so an idle proxied request is cancelled at the configured bound.
- **Per-route max request body size** (`DOCKYARP_MAX_BODY_SIZE`, bytes) enforced by a route-aware
  middleware that sets the request's max body size before proxying.

## Capabilities

### Modified Capabilities
- `docker-discovery`: proxy-tuning labels set a cluster timeout and a route body-size limit.
- `proxy-routing`: the model carries a cluster request timeout and a route body-size limit.
- `yarp-dynamic-config`: the cluster timeout maps to YARP and the body-size limit is enforced per request.

## Impact

- **Code**: `src/DockYarp.Core` (`Cluster.RequestTimeout`, `RouteRule.MaxRequestBodySize`),
  `src/DockYarp.Docker` (labels), `src/DockYarp.App/ReverseProxy` (YARP mapping),
  `src/DockYarp.App` (request body-size middleware).
- **Deferred**: response buffering (YARP streams by default), gzip/compression, and a global Kestrel body
  limit — out of scope here.
- **Owning agent**: AG-RP / AG-DD.
