## Why
nginx-proxy supports `VIRTUAL_PROTO=grpc` (and `grpcs`), forwarding gRPC over HTTP/2 to the backend. DockYarp
maps only http/https, so gRPC backends cannot be declared even though YARP proxies gRPC well (gRPC is HTTP/2
with trailers, which YARP forwards).

## What Changes
- `VIRTUAL_PROTO=grpc` selects the http backend scheme and `grpcs` the https scheme, and both force the cluster
  to contact the backend over **HTTP/2 exactly** (`HttpRequest.Version = 2.0`,
  `VersionPolicy = RequestVersionExact`) — YARP then carries gRPC (including trailers).
- `grpc`/`grpcs` are recognized `VIRTUAL_PROTO` values (no longer warned as unsupported).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: a backend may be declared as gRPC (`VIRTUAL_PROTO=grpc`/`grpcs`), causing the cluster to
  be reached over HTTP/2.

## Impact
- **Code**: `DockYarp.Core` (`Cluster.Http2Only`), `DockYarp.Docker` (`LabelParser` grpc/grpcs scheme + http2 flag,
  `ContainerLabelConfig.Http2`, `ContainerMapper` classic cluster), `DockYarp.App` (`YarpConfigMapper` request
  version).
- **Tests**: `LabelParser` (grpc→http+http2, grpcs→https+http2, grpc/grpcs not "unsupported"), `YarpConfigMapper`
  (an http2 cluster maps to `Version 2.0` + `RequestVersionExact`).
- **Deferred**: an end-to-end gRPC round-trip needs a gRPC backend fixture in the Aspire suite — tracked as a new
  backlog item `e2e-grpc-passthrough`. Plaintext (h2c) gRPC **inbound** is out of scope (the plaintext endpoint is
  HTTP/1.1 only); gRPC clients use TLS (HTTP/2) inbound. Multiports gRPC is not wired (classic `VIRTUAL_PROTO`
  only).
- **Owning agent**: AG-RP. Resolves `add-grpc-passthrough`.
